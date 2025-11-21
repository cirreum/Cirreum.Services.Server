namespace Cirreum.Diagnostics;

using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

/// <summary>
/// Adds the ExceptionModelJsonContext to the current JsonSerializerOptions.
///
/// This allows for consistent serialization behavior for ProblemDetails regardless if
/// the default reflection-based serializer is used or not. And makes it trim/NativeAOT compatible.
/// </summary>
internal sealed class ExceptionModelJsonOptionsSetup : IConfigureOptions<JsonOptions> {
	public void Configure(JsonOptions options) {
		// Always insert the ExceptionModelJsonContext to the beginning of the chain at the time
		// this Configure is invoked. This JsonTypeInfoResolver will be before the default reflection-based resolver,
		// and before any other resolvers currently added.
		// If apps need to customize ProblemDetails serialization, they can prepend a custom ProblemDetails resolver
		// to the chain in an IConfigureOptions<JsonOptions> registered after the call to AddProblemDetails().
		options.SerializerOptions.TypeInfoResolverChain.Insert(0, new ExceptionModelJsonContext());
	}
}