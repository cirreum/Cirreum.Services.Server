namespace Cirreum.Conductor.Caching;

using Microsoft.Extensions.Caching.Hybrid;
using System.Collections.Generic;

public sealed class HybridCacheableQueryService(HybridCache hybridCache) : ICacheableQueryService {

	private static HybridCacheEntryOptions CreateOptions(QueryCacheSettings settings, bool useFailureExpiration = false) {
		var expiration = useFailureExpiration && settings.FailureExpiration.HasValue
			? settings.FailureExpiration.Value
			: settings.Expiration;

		var localExpiration = useFailureExpiration && settings.FailureExpiration.HasValue
			? settings.FailureExpiration.Value
			: settings.LocalExpiration;

		return new HybridCacheEntryOptions {
			Expiration = expiration,
			LocalCacheExpiration = localExpiration
		};
	}

	public async ValueTask<TResponse> GetOrCreateAsync<TResponse>(
		string cacheKey,
		Func<CancellationToken, ValueTask<TResponse>> factory,
		QueryCacheSettings settings,
		string[]? tags = null,
		CancellationToken cancellationToken = default) {

		var value = await hybridCache.GetOrCreateAsync(
			cacheKey,
			factory,
			options: CreateOptions(settings),  // Start with normal options
			tags: tags,
			cancellationToken: cancellationToken);

		// If it's a failed Result and we have a FailureExpiration, update with shorter duration
		if (value is IResult { IsSuccess: false } && settings.FailureExpiration.HasValue) {
			await hybridCache.SetAsync(
				cacheKey,
				value,
				CreateOptions(settings, useFailureExpiration: true),
				tags: tags,
				cancellationToken: cancellationToken);
		}

		return value;

	}

	public ValueTask RemoveAsync(string cacheKey, CancellationToken cancellationToken) {
		return hybridCache.RemoveAsync(cacheKey, cancellationToken);
	}

	public ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default) {
		return hybridCache.RemoveByTagAsync(tag, cancellationToken);
	}

	public ValueTask RemoveByTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default) {
		return hybridCache.RemoveByTagAsync(tags, cancellationToken);
	}

}