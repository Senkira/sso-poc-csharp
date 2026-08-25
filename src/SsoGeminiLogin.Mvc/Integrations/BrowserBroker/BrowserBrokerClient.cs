using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using SsoGeminiLogin.Mvc.Integrations.Interfaces;

namespace SsoGeminiLogin.Mvc.Integrations.BrowserBroker;

public sealed class BrowserBrokerClient(HttpClient httpClient, IOptions<BrowserBrokerOptions> options) : IBrowserBrokerClient
{
	private sealed record AccountProviderResponse(string Id, string Email);

	private sealed record BrokerProblemDetails(string? Title);

	private sealed record AccountMappingEnvelope(AccountProviderResponse Account);

	private sealed record BrowserSessionCreatedProviderResponse(string SessionId, string Status);

	private sealed record BrowserAgentProgressProviderResponse(string Status, string Message, DateTimeOffset At);

	private sealed record BrowserViewportProviderResponse(int Width, int Height);

	private sealed record BrowserSessionProviderResponse(string SessionId, string Status, string? Message, string? Account, DateTimeOffset? CreatedAt, BrowserViewportProviderResponse? Viewport, IReadOnlyList<BrowserAgentProgressProviderResponse>? Progress, bool AccountVerified, bool? OpenAllowed);

	private const string GatewayKeyHeader = "X-Gateway-Key";

	private const string SsoUserHeader = "X-SSO-User";

	private readonly BrowserBrokerOptions _options = options.Value;

	public async Task<bool> IsReadyAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		try
		{
			using HttpResponseMessage httpResponseMessage = await httpClient.GetAsync("/health/ready", cancellationToken);
			return httpResponseMessage.IsSuccessStatusCode;
		}
		catch (HttpRequestException)
		{
			return false;
		}
		catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			return false;
		}
	}

	public async Task<BrowserAccount> GetAccountAsync(string ssoUser, CancellationToken cancellationToken = default(CancellationToken))
	{
		AccountMappingEnvelope accountMappingEnvelope = await SendAsync<AccountMappingEnvelope>(HttpMethod.Get, "/api/v1/account-mappings/current", ssoUser, cancellationToken);
		return new BrowserAccount(accountMappingEnvelope.Account.Id, accountMappingEnvelope.Account.Email);
	}

	public async Task<BrowserSessionState> CreateSessionAsync(string ssoUser, CancellationToken cancellationToken = default(CancellationToken))
	{
		BrowserSessionCreatedProviderResponse browserSessionCreatedProviderResponse = await SendAsync<BrowserSessionCreatedProviderResponse>(HttpMethod.Post, "/api/v1/browser-sessions", ssoUser, cancellationToken);
		return new BrowserSessionState(browserSessionCreatedProviderResponse.SessionId, browserSessionCreatedProviderResponse.Status, null, null, null, null, Array.Empty<BrowserAgentProgress>(), AccountVerified: false, CanOpen: false);
	}

	public async Task<BrowserSessionState?> GetCurrentSessionAsync(string ssoUser, CancellationToken cancellationToken = default(CancellationToken))
	{
		HttpResponseMessage httpResponseMessage = await SendRequestAsync(HttpMethod.Get, "/api/v1/browser-sessions/current", ssoUser, cancellationToken);
		using (httpResponseMessage)
		{
			if (httpResponseMessage.StatusCode == HttpStatusCode.NotFound)
			{
				return null;
			}
			return ToState(await ReadRequiredAsync<BrowserSessionProviderResponse>(httpResponseMessage, cancellationToken));
		}
	}

	public async Task<BrowserSessionState?> GetSessionAsync(string sessionId, string ssoUser, CancellationToken cancellationToken = default(CancellationToken))
	{
		ValidateSessionId(sessionId);
		HttpResponseMessage httpResponseMessage = await SendRequestAsync(HttpMethod.Get, "/api/v1/browser-sessions/" + Uri.EscapeDataString(sessionId), ssoUser, cancellationToken);
		using (httpResponseMessage)
		{
			if (httpResponseMessage.StatusCode == HttpStatusCode.NotFound)
			{
				return null;
			}
			return ToState(await ReadRequiredAsync<BrowserSessionProviderResponse>(httpResponseMessage, cancellationToken));
		}
	}

	public async Task<BrowserSessionState> OpenSessionAsync(string sessionId, string ssoUser, CancellationToken cancellationToken = default(CancellationToken))
	{
		ValidateSessionId(sessionId);
		return ToState(await SendAsync<BrowserSessionProviderResponse>(HttpMethod.Post, "/api/v1/browser-sessions/" + Uri.EscapeDataString(sessionId) + "/open", ssoUser, cancellationToken));
	}

	public async Task EndSessionAsync(string sessionId, string ssoUser, CancellationToken cancellationToken = default(CancellationToken))
	{
		ValidateSessionId(sessionId);
		using HttpResponseMessage response = await SendRequestAsync(HttpMethod.Delete, "/api/v1/browser-sessions/" + Uri.EscapeDataString(sessionId), ssoUser, cancellationToken);
		if (!response.IsSuccessStatusCode)
		{
			throw await CreateStatusExceptionAsync(response, cancellationToken);
		}
	}

	private async Task<TResponse> SendAsync<TResponse>(HttpMethod method, string path, string ssoUser, CancellationToken cancellationToken)
	{
		using HttpResponseMessage response = await SendRequestAsync(method, path, ssoUser, cancellationToken);
		return await ReadRequiredAsync<TResponse>(response, cancellationToken);
	}

	private async Task<HttpResponseMessage> SendRequestAsync(HttpMethod method, string path, string ssoUser, CancellationToken cancellationToken)
	{
		using HttpRequestMessage request = new HttpRequestMessage(method, path);
		request.Headers.Add("X-Gateway-Key", _options.GatewayKey);
		request.Headers.Add("X-SSO-User", NormalizeUser(ssoUser));
		request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
		try
		{
			return await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
		}
		catch (HttpRequestException ex)
		{
			Exception innerException = ex;
			throw new BrowserBrokerException("The browser broker API is unavailable.", null, innerException, isPublic: true);
		}
		catch (TaskCanceledException innerException2) when (!cancellationToken.IsCancellationRequested)
		{
			throw new BrowserBrokerException("The browser broker API request timed out.", null, innerException2, isPublic: true);
		}
	}

	private async Task<TResponse> ReadRequiredAsync<TResponse>(HttpResponseMessage response, CancellationToken cancellationToken)
	{
		if (!response.IsSuccessStatusCode)
		{
			throw await CreateStatusExceptionAsync(response, cancellationToken);
		}
		string? a = response.Content.Headers.ContentType?.MediaType;
		if (!string.Equals(a, "application/json", StringComparison.OrdinalIgnoreCase))
		{
			throw new BrowserBrokerException("The browser broker API returned an unsupported content type.", null, null, isPublic: true);
		}
		if (response.Content.Headers.ContentLength > _options.MaximumResponseBytes)
		{
			throw new BrowserBrokerException("The browser broker API response exceeded the configured size limit.", null, null, isPublic: true);
		}
		try
		{
			await response.Content.LoadIntoBufferAsync(_options.MaximumResponseBytes, cancellationToken);
			TResponse? val = await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken);
			if (val == null)
			{
				throw new BrowserBrokerException("The browser broker API returned an invalid response.", null, null, isPublic: true);
			}
			return val;
		}
		catch (JsonException ex)
		{
			Exception innerException = ex;
			throw new BrowserBrokerException("The browser broker API returned a malformed response.", null, innerException, isPublic: true);
		}
		catch (HttpRequestException ex2)
		{
			Exception innerException = ex2;
			throw new BrowserBrokerException("The browser broker API response could not be read safely.", null, innerException, isPublic: true);
		}
	}

	private static BrowserSessionState ToState(BrowserSessionProviderResponse response)
	{
		return new BrowserSessionState(response.SessionId, response.Status, response.Message, response.Account, response.CreatedAt, response.Viewport is null ? null : new BrowserViewport(response.Viewport.Width, response.Viewport.Height), response.Progress?.Select((BrowserAgentProgressProviderResponse progress) => new BrowserAgentProgress(progress.Status, progress.Message, progress.At)).ToArray() ?? Array.Empty<BrowserAgentProgress>(), response.AccountVerified, response.OpenAllowed == true);
	}

	private static string NormalizeUser(string ssoUser)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(ssoUser, nameof(ssoUser));
		return ssoUser.Trim().ToLowerInvariant();
	}

	private static void ValidateSessionId(string sessionId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sessionId, nameof(sessionId));
		if (!sessionId.StartsWith("gbs_", StringComparison.Ordinal) || sessionId.Length > 64)
		{
			throw new ArgumentException("The browser session identifier is invalid.", nameof(sessionId));
		}
	}

	private async Task<BrowserBrokerException> CreateStatusExceptionAsync(HttpResponseMessage response, CancellationToken cancellationToken)
	{
		string message = "The browser broker API rejected the request.";
		long? contentLength = response.Content.Headers.ContentLength;
		if (contentLength.HasValue)
		{
			long valueOrDefault = contentLength.GetValueOrDefault();
			if (valueOrDefault > _options.MaximumResponseBytes)
			{
				goto IL_0125;
			}
		}
		if (string.Equals(response.Content.Headers.ContentType?.MediaType, "application/problem+json", StringComparison.OrdinalIgnoreCase))
		{
			try
			{
				BrokerProblemDetails? brokerProblemDetails = await response.Content.ReadFromJsonAsync<BrokerProblemDetails>(cancellationToken);
				if (!string.IsNullOrWhiteSpace(brokerProblemDetails?.Title))
				{
					message = brokerProblemDetails.Title;
				}
			}
			catch (JsonException)
			{
			}
		}
		goto IL_0125;
		IL_0125:
		return new BrowserBrokerException(message, response.StatusCode, null, isPublic: true);
	}
}
