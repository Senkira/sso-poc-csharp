using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using SsoGeminiLogin.Api.Configuration;
using SsoGeminiLogin.Api.Services.Models;

namespace SsoGeminiLogin.Api.Services;

public sealed class BrowserSessionStore(TimeProvider timeProvider, IOptions<BrowserSessionOptions> options)
{
	private readonly object _gate = new object();

	private readonly ConcurrentDictionary<string, BrowserSession> _sessions = new ConcurrentDictionary<string, BrowserSession>(StringComparer.Ordinal);

	private readonly ConcurrentDictionary<string, string> _sessionIdByUser = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

	private readonly TimeSpan _timeToLive = TimeSpan.FromMinutes(options.Value.TtlMinutes);

	public BrowserSession GetOrCreate(string user, string accountId)
	{
		lock (_gate)
		{
			if (_sessionIdByUser.TryGetValue(user, out var value) && _sessions.TryGetValue(value, out var value2))
			{
				if (!IsExpired(value2))
				{
					return value2;
				}
				RemoveCore(value2);
			}
			string text = "gbs_" + WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
			DateTimeOffset utcNow = timeProvider.GetUtcNow();
			BrowserSession browserSession = new BrowserSession(text, user, accountId, utcNow, utcNow.Add(_timeToLive));
			_sessions[text] = browserSession;
			_sessionIdByUser[user] = text;
			return browserSession;
		}
	}

	public BrowserSession? FindCurrent(string user)
	{
		lock (_gate)
		{
			if (!_sessionIdByUser.TryGetValue(user, out var value) || !_sessions.TryGetValue(value, out var value2))
			{
				return null;
			}
			if (!IsExpired(value2))
			{
				return value2;
			}
			RemoveCore(value2);
			return null;
		}
	}

	public BrowserSession? FindOwned(string sessionId, string user)
	{
		lock (_gate)
		{
			if (!_sessions.TryGetValue(sessionId, out var value) || !string.Equals(value.User, user, StringComparison.OrdinalIgnoreCase))
			{
				return null;
			}
			if (!IsExpired(value))
			{
				return value;
			}
			RemoveCore(value);
			return null;
		}
	}

	public bool Remove(BrowserSession session)
	{
		lock (_gate)
		{
			return RemoveCore(session);
		}
	}

	private bool IsExpired(BrowserSession session)
	{
		return timeProvider.GetUtcNow() >= session.ExpiresAt;
	}

	private bool RemoveCore(BrowserSession session)
	{
		_sessionIdByUser.TryRemove(new KeyValuePair<string, string>(session.User, session.Id));
		return _sessions.TryRemove(session.Id, out _);
	}
}
