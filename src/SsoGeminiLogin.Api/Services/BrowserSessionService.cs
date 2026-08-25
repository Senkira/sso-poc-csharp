using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using SsoGeminiLogin.Api.Integrations.Interfaces;
using SsoGeminiLogin.Api.Services.Interfaces;
using SsoGeminiLogin.Api.Services.Models;

namespace SsoGeminiLogin.Api.Services;

public sealed class BrowserSessionService(BrowserSessionStore sessions, IBrowserAccountMappingRepository accountMappings, IBrowserAgent browserAgent) : IBrowserSessionService
{
	private static readonly SearchValues<char> SessionIdCharacters = SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_".AsSpan());

	public Task<AccountMappingResult?> GetAccountMappingAsync(string user, CancellationToken cancellationToken = default(CancellationToken))
	{
		return accountMappings.FindActiveBySsoUserAsync(NormalizeUser(user), cancellationToken);
	}

	public async Task<BrowserSessionResult?> CreateAsync(string user, CancellationToken cancellationToken = default(CancellationToken))
	{
		string normalizedUser = NormalizeUser(user);
		AccountMappingResult? mapping = await accountMappings.FindActiveBySsoUserAsync(normalizedUser, cancellationToken);
		if (mapping is null)
		{
			return null;
		}
		BrowserAgentStatus status = await browserAgent.StartAsync(normalizedUser, mapping.Account, cancellationToken);
		BrowserSession orCreate = sessions.GetOrCreate(normalizedUser, mapping.Account.Id);
		return ToResult(orCreate.Id, status);
	}

	public async Task<BrowserSessionResult?> GetCurrentAsync(string user, CancellationToken cancellationToken = default(CancellationToken))
	{
		string user2 = NormalizeUser(user);
		BrowserSession? session = sessions.FindCurrent(user2);
		if (session is null)
		{
			return null;
		}
		BrowserAgentStatus browserAgentStatus = await browserAgent.StatusAsync(user2, cancellationToken);
		return ToResult(session.Id, browserAgentStatus, browserAgentStatus.Status == "ready" && browserAgentStatus.AccountVerified);
	}

	public async Task<BrowserSessionResult?> GetAsync(string sessionId, string user, CancellationToken cancellationToken = default(CancellationToken))
	{
		string user2 = NormalizeUser(user);
		BrowserSession? session = FindOwnedSession(sessionId, user2);
		if (session is null)
		{
			return null;
		}
		BrowserAgentStatus browserAgentStatus = await browserAgent.StatusAsync(user2, cancellationToken);
		return ToResult(session.Id, browserAgentStatus, browserAgentStatus.Status == "ready" && browserAgentStatus.AccountVerified);
	}

	public async Task<BrowserSessionResult?> OpenAsync(string sessionId, string user, CancellationToken cancellationToken = default(CancellationToken))
	{
		string user2 = NormalizeUser(user);
		BrowserSession? session = FindOwnedSession(sessionId, user2);
		if (session is null)
		{
			return null;
		}
		BrowserAgentStatus status = await browserAgent.HandoffAsync(user2, cancellationToken);
		return ToResult(session.Id, status);
	}

	public async Task<bool> EndAsync(string sessionId, string user, CancellationToken cancellationToken = default(CancellationToken))
	{
		string user2 = NormalizeUser(user);
		BrowserSession? session = FindOwnedSession(sessionId, user2);
		if (session is null)
		{
			return false;
		}
		await browserAgent.EndAsync(user2, cancellationToken);
		sessions.Remove(session);
		return true;
	}

	private static string NormalizeUser(string user)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(user, nameof(user));
		return user.Trim().ToLowerInvariant();
	}

	private BrowserSession? FindOwnedSession(string sessionId, string user)
	{
		if (!sessionId.StartsWith("gbs_", StringComparison.Ordinal) || sessionId.AsSpan(4).ContainsAnyExcept(SessionIdCharacters))
		{
			return null;
		}
		return sessions.FindOwned(sessionId, user);
	}

	private static BrowserSessionResult ToResult(string sessionId, BrowserAgentStatus status, bool? openAllowed = null)
	{
		return new BrowserSessionResult(sessionId, status.Status, status.Message, status.Account, status.CreatedAt, status.Viewport, status.Progress, status.AccountVerified, openAllowed);
	}
}
