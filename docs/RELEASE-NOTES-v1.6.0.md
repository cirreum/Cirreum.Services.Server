# Cirreum.Services.Server 1.6.0 — browser clients can authenticate a WebSocket connection

## Why this release exists

A browser cannot set request headers on a WebSocket upgrade. A client connecting to a hub
over WebSockets therefore sends its bearer token as an `access_token` query parameter — the
convention SignalR's own clients follow, and the only one available to them.

No Cirreum scheme read it. The upgrade arrived with no credential the server recognized and
was refused, and SignalR responded by falling back to Server-Sent Events or long polling. The
application kept working, never used WebSockets, and nothing reported the downgrade. The
failure is invisible precisely because the fallback succeeds.

## What's new

### `UseConnectionCredential()`

Middleware that promotes a query-carried bearer credential into the `Authorization` header
before authentication runs:

```csharp
app.UseRouting();
app.UseConnectionCredential();   // between routing and authentication
app.UseAuthentication();
```

`Cirreum.Runtime.Server` registers it in its default pipeline, so applications using
`UseDefaultMiddleware()` need do nothing.

The position is what makes it work. Routing must have run, because the promotion is scoped by
the endpoint; and authentication must not have run yet, because the header has to be in place
before any scheme reads it.

Promoting the credential — rather than teaching each scheme to look in a second place — is
what keeps this small. Every scheme and every scheme selector continues to read the
`Authorization` header exactly as before: ApiKey, SessionTicket, SignedRequest, External, the
audience schemes and the JWT audience-routing selector are all unchanged, and a scheme added
later inherits the behaviour without knowing it exists.

The value is promoted verbatim. A scheme prefix carried inside the credential is part of the
opaque secret its issuer minted and stored, so prefix-based dispatch keeps working.

### Scoping

The promotion applies only where a client has no alternative. Both conditions must hold:

| Condition | Why |
| --- | --- |
| The endpoint is a SignalR hub, or carries `InvocationConnectionMetadata` | Only a connection upgrade denies a client the ability to send headers |
| The request has no `Authorization` header | A header the client could send is authoritative, whatever scheme it names |

A query parameter on an ordinary route is left alone and carries no authority. So is one on a
request that arrives before routing has resolved an endpoint.

### `MapWebSocketHandler` stamps the endpoint

Endpoints mapped by `MapWebSocketHandler` now carry `InvocationConnectionMetadata` from
`Cirreum.Contracts` 4.7.0, declaring that their invocations arrive over an
`IInvocationConnection`. SignalR hubs need no stamp — `MapHub` already carries SignalR's own
hub metadata, which the middleware recognizes directly.

An application mapping a long-lived transport of its own stamps the marker to opt in:

```csharp
app.Map("/custom-stream", handler)
   .WithMetadata(InvocationConnectionMetadata.Instance);
```

## Compatibility

* **Additive.** No existing member changed, and no authentication scheme was modified.
* A request that carries an `Authorization` header behaves exactly as before.
* Applications composing their own pipeline should add `UseConnectionCredential()` between
  `UseRouting()` and `UseAuthentication()`; without it, browser WebSocket clients continue to
  fall back to Server-Sent Events or long polling as they did previously.
* Requires `Cirreum.Contracts` 4.7.0.

## See also

* `InvocationConnectionMetadata` (`Cirreum.Contracts`) — the endpoint marker the middleware reads.
* `IInvocationConnection` — the connection whose endpoints the marker identifies.
