using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SsoGeminiLogin.Api.Configuration;
using SsoGeminiLogin.Api.Integrations.Interfaces;
using SsoGeminiLogin.Api.Services.Models;

namespace SsoGeminiLogin.Api.Integrations.SimulatedBrowser;

public sealed class SimulatedBrowserAgent(IOptions<AgentOptions> options, TimeProvider timeProvider, ILogger<SimulatedBrowserAgent> logger) : IBrowserAgent, IAsyncDisposable
{
	private sealed class SimulatedWorker
	{
		private readonly object _gate = new object();

		private readonly List<AgentProgress> _progress = new List<AgentProgress>();

		private readonly string _account;

		private readonly DateTimeOffset _createdAt;

		private string _status = "starting";

		private string _message = "กำล\u0e31งสร\u0e49าง Secure Browser...";

		private bool _accountVerified;

		public string AccountId { get; }

		public string ProfileDirectory { get; }

		public CancellationTokenSource LifetimeCancellation { get; } = new CancellationTokenSource();

		public Task? LaunchTask { get; set; }

		public SimulatedWorker(string accountId, string account, string profileDirectory, DateTimeOffset createdAt)
		{
			AccountId = accountId;
			_account = account;
			ProfileDirectory = profileDirectory;
			_createdAt = createdAt;
			_progress.Add(new AgentProgress(_status, "สร\u0e49าง browser worker และจอง mapped account", _createdAt));
		}

		public void Record(string status, string message, DateTimeOffset at)
		{
			lock (_gate)
			{
				if (!LifetimeCancellation.IsCancellationRequested)
				{
					_status = status;
					_message = message;
					if (status == "ready")
					{
						_accountVerified = true;
					}
					_progress.Add(new AgentProgress(status, message, at));
				}
			}
		}

		public bool TryBeginHandoff(DateTimeOffset at)
		{
			lock (_gate)
			{
				if (_status != "ready")
				{
					return false;
				}
				_status = "handing-off";
				_message = "กำล\u0e31งเป\u0e34ด Gemini ใน Microsoft Edge หน\u0e49าจร\u0e34ง...";
				_progress.Add(new AgentProgress(_status, _message, at));
				return true;
			}
		}

		public void Cancel()
		{
			if (!LifetimeCancellation.IsCancellationRequested)
			{
				LifetimeCancellation.Cancel();
			}
		}

		public BrowserAgentStatus Snapshot()
		{
			lock (_gate)
			{
				return new BrowserAgentStatus(_status, _message, _account, _createdAt, new AgentViewport(1440, 900), _progress.TakeLast(20).ToArray(), _accountVerified);
			}
		}
	}

	private const int StepDelayMilliseconds = 120;

	private static readonly Action<ILogger, Exception?> LogSimulationFailure = LoggerMessage.Define(LogLevel.Error, new EventId(3201, "SimulatedBrowserWorkflowFailure"), "The deterministic simulated browser workflow failed.");

	private readonly object _gate = new object();

	private readonly AgentOptions _options = options.Value;

	private readonly ConcurrentDictionary<string, SimulatedWorker> _workers = new ConcurrentDictionary<string, SimulatedWorker>(StringComparer.OrdinalIgnoreCase);

	private readonly Dictionary<string, string> _accountLeases = new Dictionary<string, string>(StringComparer.Ordinal);

	public Task<BrowserAgentDescription> DescribeAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		return Task.FromResult(new BrowserAgentDescription(new BrowserAccount("simulated-agent", "simulated-agent@example.invalid"), "in-process-simulated-test-adapter"));
	}

	public Task<BrowserAgentStatus> StartAsync(string user, BrowserAccount account, CancellationToken cancellationToken = default(CancellationToken))
	{
		cancellationToken.ThrowIfCancellationRequested();
		SimulatedWorker simulatedWorker;
		lock (_gate)
		{
			if (_workers.TryGetValue(user, out var value))
			{
				return Task.FromResult(value.Snapshot());
			}
			if (_accountLeases.TryGetValue(account.Id, out var value2) && !string.Equals(value2, user, StringComparison.OrdinalIgnoreCase))
			{
				throw new AccountBusyException("บ\u0e31ญช\u0e35น\u0e35\u0e49กำล\u0e31งถ\u0e39กใช\u0e49งานโดย session อ\u0e37\u0e48น");
			}
			if (_workers.Count >= _options.MaxWorkers)
			{
				throw new BrowserCapacityException("Browser worker เต\u0e47ม กร\u0e38ณารอให\u0e49 session อ\u0e37\u0e48นส\u0e34\u0e49นส\u0e38ด");
			}
			simulatedWorker = new SimulatedWorker(account.Id, account.Email, ResolveProfileDirectory(account.Id), timeProvider.GetUtcNow());
			_workers[user] = simulatedWorker;
			_accountLeases[account.Id] = user;
		}
		simulatedWorker.LaunchTask = RunSimulationAsync(simulatedWorker);
		return Task.FromResult(simulatedWorker.Snapshot());
	}

	public Task<BrowserAgentStatus> StatusAsync(string user, CancellationToken cancellationToken = default(CancellationToken))
	{
		return Task.FromResult(_workers.TryGetValue(user, out var value) ? value.Snapshot() : new BrowserAgentStatus("not-started"));
	}

	public Task<BrowserAgentStatus> HandoffAsync(string user, CancellationToken cancellationToken = default(CancellationToken))
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (!_workers.TryGetValue(user, out var value))
		{
			return Task.FromResult(new BrowserAgentStatus("not-started"));
		}
		if (!value.TryBeginHandoff(timeProvider.GetUtcNow()))
		{
			BrowserAgentStatus browserAgentStatus = value.Snapshot();
			string status = browserAgentStatus.Status;
			if ((status == "handing-off" || status == "handed-off") ? true : false)
			{
				return Task.FromResult(browserAgentStatus);
			}
			throw new InvalidOperationException("Simulated Gemini session is not ready for browser handoff.");
		}
		value.Record("handed-off", "เป\u0e34ด Gemini ใน Microsoft Edge หน\u0e49าจร\u0e34งแล\u0e49ว", timeProvider.GetUtcNow());
		return Task.FromResult(value.Snapshot());
	}

	public Task EndAsync(string user, CancellationToken cancellationToken = default(CancellationToken))
	{
		lock (_gate)
		{
			if (_workers.TryRemove(user, out var value))
			{
				value.Cancel();
				_accountLeases.Remove(value.AccountId);
			}
		}
		return Task.CompletedTask;
	}

	public async ValueTask DisposeAsync()
	{
		Task[] launchTasks = (from worker in _workers.Values
			select worker.LaunchTask into task
			where task != null
			select task).Cast<Task>().ToArray();
		string[] array = _workers.Keys.ToArray();
		foreach (string user in array)
		{
			await EndAsync(user).ConfigureAwait(continueOnCapturedContext: false);
		}
		if (launchTasks.Length != 0)
		{
			await Task.WhenAll(launchTasks).ConfigureAwait(continueOnCapturedContext: false);
		}
	}

	private async Task RunSimulationAsync(SimulatedWorker worker)
	{
		_ = 5;
		try
		{
			Directory.CreateDirectory(worker.ProfileDirectory);
			await RecordAfterDelayAsync(worker, "launching-edge", "กำล\u0e31งเป\u0e34ด Edge แบบ isolated profile...").ConfigureAwait(continueOnCapturedContext: false);
			await RecordAfterDelayAsync(worker, "navigating-google", "เป\u0e34ดหน\u0e49า Google Sign-In แล\u0e49ว...").ConfigureAwait(continueOnCapturedContext: false);
			await RecordAfterDelayAsync(worker, "entering-email", "กำล\u0e31งส\u0e48ง mapped Google email...").ConfigureAwait(continueOnCapturedContext: false);
			await RecordAfterDelayAsync(worker, "entering-password", "กำล\u0e31งส\u0e48ง password จาก Windows Credential Manager...").ConfigureAwait(continueOnCapturedContext: false);
			await RecordAfterDelayAsync(worker, "waiting-google", "รอ Google ย\u0e37นย\u0e31นและสร\u0e49าง session...").ConfigureAwait(continueOnCapturedContext: false);
			await RecordAfterDelayAsync(worker, "ready", "Gemini พร\u0e49อมใช\u0e49งาน").ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (OperationCanceledException) when (worker.LifetimeCancellation.IsCancellationRequested)
		{
		}
		catch (Exception arg)
		{
			LogSimulationFailure(logger, arg);
			worker.Record("error", "ไม\u0e48สามารถเป\u0e34ด Secure Browser ได\u0e49", timeProvider.GetUtcNow());
		}
	}

	private async Task RecordAfterDelayAsync(SimulatedWorker worker, string status, string message)
	{
		await Task.Delay(120, worker.LifetimeCancellation.Token).ConfigureAwait(continueOnCapturedContext: false);
		worker.Record(status, message, timeProvider.GetUtcNow());
	}

	private string ResolveProfileDirectory(string accountId)
	{
		string path = (Path.IsPathRooted(_options.ProfileRoot) ? _options.ProfileRoot : Path.Combine(Directory.GetCurrentDirectory(), _options.ProfileRoot));
		return Path.GetFullPath(Path.Combine(path, accountId));
	}
}
