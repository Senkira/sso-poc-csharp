using System.Collections.Generic;

namespace SsoGeminiLogin.Api.Configuration;

public sealed class MockDatabaseOptions
{
	public const string SectionName = "MockDatabase";

	public string Provider { get; set; } = "AppSettings";

	public int SimulatedLatencyMilliseconds { get; set; } = 25;

	public List<MockAccountMappingOptions> AccountMappings { get; set; } = new List<MockAccountMappingOptions>();
}

