using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace KitLib.Mcp;

/// <summary>
/// Pure MCP protocol helpers: revision negotiation, live-session tracking, JSON-RPC envelopes and
/// the optional bearer-token check. Kept free of Godot/HTTP types so unit tests can link this file
/// directly (<c>tests/KitLib.DevTools.Tests</c>).
/// </summary>
internal static class McpProtocol {
    /// <summary>MCP revisions this server can negotiate, newest first.</summary>
    public static readonly string[] SupportedProtocolVersions = [
        "2026-07-28",
        "2025-11-25",
        "2025-06-18",
        "2025-03-26",
    ];

    public static bool IsSupported(string? version) {
        if (string.IsNullOrEmpty(version))
            return false;
        foreach (var supported in SupportedProtocolVersions) {
            if (string.Equals(supported, version, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Echo a supported requested revision. Unknown or missing requests fall back to the oldest
    /// supported revision (2025-03-26) rather than the newest one, so older SDKs (Claude's 1.x
    /// streamable-HTTP client, legacy MCP revisions) still negotiate instead of failing.
    /// </summary>
    public static string Negotiate(string? requested) {
        if (IsSupported(requested))
            return requested!;
        return SupportedProtocolVersions[^1];
    }
}

/// <summary>Bounded LRU of live MCP session ids; the least recently used session is evicted first.</summary>
internal sealed class McpSessionStore {
    private const int Capacity = 64;

    private readonly object _gate = new();
    private readonly LinkedList<string> _order = new();
    private readonly Dictionary<string, LinkedListNode<string>> _nodes = new(StringComparer.Ordinal);

    public int Count {
        get {
            lock (_gate)
                return _nodes.Count;
        }
    }

    public bool Contains(string sessionId) {
        lock (_gate)
            return _nodes.ContainsKey(sessionId);
    }

    /// <summary>Move an existing session to the most-recently-used end.</summary>
    public void Touch(string sessionId) {
        lock (_gate) {
            if (!_nodes.TryGetValue(sessionId, out var node))
                return;
            _order.Remove(node);
            _order.AddLast(node);
        }
    }

    public void Add(string sessionId) {
        lock (_gate) {
            if (_nodes.TryGetValue(sessionId, out var existing)) {
                _order.Remove(existing);
            }

            _nodes[sessionId] = _order.AddLast(sessionId);
            while (_nodes.Count > Capacity && _order.First is { } oldest) {
                _order.RemoveFirst();
                _nodes.Remove(oldest.Value);
            }
        }
    }

    public void Clear() {
        lock (_gate) {
            _nodes.Clear();
            _order.Clear();
        }
    }
}

/// <summary>JSON-RPC 2.0 envelope helpers shared by the MCP and legacy endpoints.</summary>
internal static class McpJsonRpc {
    public static string Result(JsonNode? id, JsonNode result) {
        var response = new JsonObject {
            ["jsonrpc"] = "2.0",
            ["id"] = id?.DeepClone(),
            ["result"] = result,
        };
        return response.ToJsonString();
    }

    public static string Error(JsonNode? id, int code, string message) {
        var response = new JsonObject {
            ["jsonrpc"] = "2.0",
            ["id"] = id?.DeepClone(),
            ["error"] = new JsonObject {
                ["code"] = code,
                ["message"] = message,
            },
        };
        return response.ToJsonString();
    }
}

/// <summary>Optional bearer-token gate; disabled when no token is configured.</summary>
internal static class McpAuth {
    private const string BearerPrefix = "Bearer ";

    public static bool IsAuthorized(string? expectedToken, string? authorizationHeader) {
        if (string.IsNullOrEmpty(expectedToken))
            return true;
        if (string.IsNullOrEmpty(authorizationHeader) ||
            !authorizationHeader.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase)) {
            return false;
        }

        var provided = authorizationHeader[BearerPrefix.Length..].Trim();
        // Loopback-only dev bridge: compare fixed-length digests so a byte-comparison side channel
        // does not matter either way.
        return CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(expectedToken)),
            SHA256.HashData(Encoding.UTF8.GetBytes(provided)));
    }
}
