using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SsoGeminiLogin.Api.Configuration;
using SsoGeminiLogin.Api.Integrations.Interfaces;
using SsoGeminiLogin.Api.Services.Models;

namespace SsoGeminiLogin.Api.Integrations.AgentProcess;

public sealed class AgentProcessBrowserAgent : IBrowserAgent, IAsyncDisposable
{
	private sealed record AgentRequest(long Id, string Method, string? User = null, string? AccountId = null);

	private sealed record AgentResponse(long Id, bool Ok, JsonElement Result, string? Error);

	private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

	private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

	private static readonly Action<ILogger, string, Exception?> LogAgentMessage = LoggerMessage.Define<string>(LogLevel.Information, new EventId(3201, "AgentProcessMessage"), "Local Browser Agent: {Message}");

	private static readonly Action<ILogger, int, Exception?> LogAgentExit = LoggerMessage.Define<int>(LogLevel.Warning, new EventId(3202, "AgentProcessExit"), "Local Browser Agent exited with code {ExitCode}.");

	private readonly object _gate = new object();

	private readonly SemaphoreSlim _requestLock = new SemaphoreSlim(1, 1);

	private readonly AgentOptions _options;

	private readonly IWebHostEnvironment _environment;

	private readonly ILogger<AgentProcessBrowserAgent> _logger;

	private readonly Queue<string> _standardError = new Queue<string>();

	private Process? _process;

	private long _nextId;

	private bool _disposed;

	public AgentProcessBrowserAgent(IOptions<AgentOptions> options, IWebHostEnvironment environment, ILogger<AgentProcessBrowserAgent> logger)
	{
		_options = options.Value;
		_environment = environment;
		_logger = logger;
	}

	public async Task<BrowserAgentDescription> DescribeAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		return await RequestAsync<BrowserAgentDescription>(new AgentRequest(NextId(), "describe"), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	public async Task<BrowserAgentStatus> StartAsync(string user, BrowserAccount account, CancellationToken cancellationToken = default(CancellationToken))
	{
		return await RequestAsync<BrowserAgentStatus>(new AgentRequest(NextId(), "start", user, account.Id), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	public async Task<BrowserAgentStatus> StatusAsync(string user, CancellationToken cancellationToken = default(CancellationToken))
	{
		return await RequestAsync<BrowserAgentStatus>(new AgentRequest(NextId(), "status", user), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	public async Task<BrowserAgentStatus> HandoffAsync(string user, CancellationToken cancellationToken = default(CancellationToken))
	{
		return await RequestAsync<BrowserAgentStatus>(new AgentRequest(NextId(), "handoff", user), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	public async Task EndAsync(string user, CancellationToken cancellationToken = default(CancellationToken))
	{
		lock (_gate)
		{
			Process? process = _process;
			if (process == null || process.HasExited)
			{
				return;
			}
		}
		await RequestAsync<JsonElement>(new AgentRequest(NextId(), "end", user), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}
		_disposed = true;
		await _requestLock.WaitAsync().ConfigureAwait(continueOnCapturedContext: false);
		try
		{
			Process? process;
			lock (_gate)
			{
				process = _process;
				_process = null;
			}
			if (process == null)
			{
				return;
			}
			try
			{
				process.StandardInput.Close();
				if (!process.WaitForExit(5000))
				{
					process.Kill(entireProcessTree: true);
					process.WaitForExit(5000);
				}
			}
			finally
			{
				process.Dispose();
			}
		}
		finally
		{
			_requestLock.Release();
			_requestLock.Dispose();
		}
	}

	private async Task<T> RequestAsync<T>(AgentRequest request, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		await _requestLock.WaitAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		try
		{
			Process process = EnsureProcess();
			string text = JsonSerializer.Serialize(request, SerializerOptions);
			await process.StandardInput.WriteLineAsync(text.AsMemory(), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			await process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			string? text2 = await process.StandardOutput.ReadLineAsync(cancellationToken).AsTask().WaitAsync(TimeSpan.FromSeconds(120L), cancellationToken)
				.ConfigureAwait(continueOnCapturedContext: false);
			if (text2 == null)
			{
				throw UnavailableFromProcess(process);
			}
			AgentResponse agentResponse = JsonSerializer.Deserialize<AgentResponse>(text2, SerializerOptions) ?? throw new BrowserAgentUnavailableException("Local Browser Agent returned an empty response.");
			if (agentResponse.Id != request.Id)
			{
				throw new BrowserAgentUnavailableException("Local Browser Agent response ID did not match the request.");
			}
			if (!agentResponse.Ok)
			{
				throw CreateRemoteException(agentResponse.Error);
			}
			T? val = agentResponse.Result.Deserialize<T>(SerializerOptions);
			if (val == null)
			{
				throw new BrowserAgentUnavailableException("Local Browser Agent returned an invalid result.");
			}
			return val;
		}
		catch (BrowserAgentUnavailableException)
		{
			ResetFailedProcess();
			throw;
		}
		catch (IOException innerException)
		{
			ResetFailedProcess();
			throw new BrowserAgentUnavailableException("Local Browser Agent IPC failed. " + RecentError(), innerException);
		}
		catch (TimeoutException innerException2)
		{
			ResetFailedProcess();
			throw new BrowserAgentUnavailableException("Local Browser Agent did not respond in time.", innerException2);
		}
		finally
		{
			_requestLock.Release();
		}
	}

	private Process EnsureProcess()
	{
		Process? process;
		lock (_gate)
		{
			process = _process;
			if (process != null && !process.HasExited)
			{
				process = _process;
			}
			else
			{
				_process?.Dispose();
				_process = null;
				_standardError.Clear();
				string text = ResolveExecutablePath();
				ProcessStartInfo processStartInfo = new ProcessStartInfo
				{
					FileName = text,
					WorkingDirectory = (Path.GetDirectoryName(text) ?? _environment.ContentRootPath),
					UseShellExecute = false,
					CreateNoWindow = true,
					RedirectStandardInput = true,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					StandardInputEncoding = Utf8WithoutBom,
					StandardOutputEncoding = Utf8WithoutBom,
					StandardErrorEncoding = Utf8WithoutBom
				};
				processStartInfo.Environment["POC_CREDENTIAL_TARGET"] = _options.CredentialTarget;
				processStartInfo.Environment["MAX_BROWSER_WORKERS"] = _options.MaxWorkers.ToString(CultureInfo.InvariantCulture);
				processStartInfo.Environment["BROWSER_HEADLESS"] = (_options.Headless ? "true" : "false");
				if (!string.IsNullOrWhiteSpace(_options.EdgeExecutable))
				{
					processStartInfo.Environment["EDGE_EXECUTABLE"] = _options.EdgeExecutable;
				}
				if (!string.IsNullOrWhiteSpace(_options.ProfileRoot))
				{
					processStartInfo.Environment["BROWSER_PROFILE_ROOT"] = _options.ProfileRoot;
				}
				Process process2 = new Process
				{
					StartInfo = processStartInfo,
					EnableRaisingEvents = true
				};
				process2.ErrorDataReceived += delegate(object _, DataReceivedEventArgs args)
				{
					CaptureError(args.Data);
				};
				process2.Exited += delegate
				{
					LogAgentExit(_logger, process2.ExitCode, null);
				};
				if (!process2.Start())
				{
					process2.Dispose();
					throw new BrowserAgentUnavailableException("Local Browser Agent could not be started.");
				}
				process2.BeginErrorReadLine();
				_process = process2;
				process = process2;
			}
		}
		return process ?? throw new BrowserAgentUnavailableException("Local Browser Agent could not be started.");
	}

	private string ResolveExecutablePath()
	{
		List<string> list = new List<string>();
		if (!string.IsNullOrWhiteSpace(_options.ExecutablePath))
		{
			list.Add(Path.IsPathRooted(_options.ExecutablePath) ? _options.ExecutablePath : Path.Combine(_environment.ContentRootPath, _options.ExecutablePath));
		}
		list.Add(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "agent", "SsoGeminiLogin.Agent.exe")));
		list.Add(Path.Combine(AppContext.BaseDirectory, "agent", "SsoGeminiLogin.Agent.exe"));
		string? text = list.FirstOrDefault(File.Exists);
		return text ?? throw new BrowserAgentUnavailableException("Local Browser Agent executable was not found. Publish the Agent with the API bundle.");
	}

	private void CaptureError(string? message)
	{
		if (string.IsNullOrWhiteSpace(message))
		{
			return;
		}
		LogAgentMessage(_logger, message, null);
		lock (_gate)
		{
			_standardError.Enqueue(message);
			while (_standardError.Count > 10)
			{
				_standardError.Dequeue();
			}
		}
	}

	private BrowserAgentUnavailableException UnavailableFromProcess(Process process)
	{
		if (!process.HasExited)
		{
			process.WaitForExit(1000);
		}
		string text = RecentError();
		return new BrowserAgentUnavailableException(string.IsNullOrWhiteSpace(text) ? "Local Browser Agent stopped before it returned a response." : text);
	}

	private string RecentError()
	{
		lock (_gate)
		{
			return (_standardError.Count == 0) ? string.Empty : string.Join(" ", _standardError);
		}
	}

	private void ResetFailedProcess()
	{
		lock (_gate)
		{
			if (_process == null)
			{
				return;
			}
			try
			{
				if (!_process.HasExited)
				{
					_process.Kill(entireProcessTree: true);
				}
			}
			catch (InvalidOperationException)
			{
			}
			_process.Dispose();
			_process = null;
		}
	}

	private static Exception CreateRemoteException(string? error)
	{
		string text = (string.IsNullOrWhiteSpace(error) ? "Local Browser Agent rejected the request." : error);
		if (text.Contains("session อ\u0e37\u0e48น", StringComparison.Ordinal))
		{
			return new AccountBusyException(text);
		}
		if (text.Contains("worker เต\u0e47ม", StringComparison.Ordinal))
		{
			return new BrowserCapacityException(text);
		}
		return new InvalidOperationException(text);
	}

	private long NextId()
	{
		return Interlocked.Increment(ref _nextId);
	}
}
