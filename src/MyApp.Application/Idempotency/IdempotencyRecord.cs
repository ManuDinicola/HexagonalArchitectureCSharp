namespace MyApp.Application.Idempotency;

/// <summary>What the first successful execution of an idempotent command left behind: enough to answer a retry without running it again.</summary>
/// <param name="Scope">The command type, so the same key can be used for different operations.</param>
/// <param name="Key">The caller's idempotency key.</param>
/// <param name="Fingerprint">Hash of the request body, to tell a retry from a different request that reuses the key.</param>
/// <param name="Response">The successful value, serialized.</param>
/// <param name="CreatedAt">When the command first succeeded.</param>
public sealed record IdempotencyRecord(string Scope, string Key, string Fingerprint, string Response, DateTimeOffset CreatedAt);
