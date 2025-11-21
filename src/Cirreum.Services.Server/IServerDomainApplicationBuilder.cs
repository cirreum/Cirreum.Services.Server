namespace Cirreum;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

public interface IServerDomainApplicationBuilder : IDomainApplicationBuilder {

	/// <summary>
	/// Gets the set of key/value configuration properties.
	/// </summary>
	/// <remarks>
	/// This can be mutated by adding more configuration sources, which will update its current view.
	/// </remarks>
	IConfigurationManager Configuration { get; }

	/// <summary>
	/// Gets the information about the hosting environment an application is running in.
	/// </summary>
	IHostEnvironment Environment { get; }

	/// <summary>
	/// Get all logged messages.
	/// </summary>
	/// <returns>A collection of any deferred logged messages.</returns>
	IEnumerable<string> GetDeferredLogMessages();

}