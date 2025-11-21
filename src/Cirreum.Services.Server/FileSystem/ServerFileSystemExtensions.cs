namespace Cirreum.FileSystem;

/// <summary>
/// Extension method for an implementation of <see cref="IFileSystem"/>.
/// </summary>
public static class ServerFileSystemExtensions {

	/// <summary>
	/// The default buffer size: 4096 bytes
	/// </summary>
	public const int DefaultBufferSize = 4096;

	/// <summary>
	/// Creates a file in a particular path.  If the file exists, it is replaced
	/// </summary>
	/// <param name="fs">The <see cref="IFileSystem"/> instance.</param>
	/// <param name="path">The path of where to create the file.</param>
	/// <returns>A <see cref="FileStream"/> opened to the file path specifed.</returns>
	/// <remarks>
	/// The file is opened with ReadWrite access and cannot be opened by another
	/// application until it has been closed.  An IOException is thrown if the
	/// directory specified doesn't exist.
	/// </remarks>
	public static FileStream Create(this IFileSystem fs, string path)
		=> Create(fs, path, DefaultBufferSize);

	/// <summary>
	/// Creates a file in a particular path.  If the file exists, it is replaced.
	/// </summary>
	/// <param name="fs">The <see cref="IFileSystem"/> instance.</param>
	/// <param name="path">The path of where to create the file.</param>
	/// <param name="bufferSize">Size of the buffer. Default: 4096</param>
	/// <returns>A <see cref="FileStream"/> opened to the file path specifed.</returns>
	/// <remarks>
	/// The file is opened with ReadWrite access and cannot be opened by another
	/// application until it has been closed.  An IOException is thrown if the
	/// directory specified doesn't exist.
	/// </remarks>
#pragma warning disable IDE0060 // Remove unused parameter
	public static FileStream Create(this IFileSystem fs, string path, int bufferSize = DefaultBufferSize)
#pragma warning restore IDE0060 // Remove unused parameter
		=> new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None, bufferSize);

	/// <summary>
	/// Creates a file in a particular path.  If the file exists, it is replaced.
	/// </summary>
	/// <param name="fs">The <see cref="IFileSystem"/> instance.</param>
	/// <param name="path">The path of where to create the file.</param>
	/// <param name="bufferSize">Size of the buffer.</param>
	/// <param name="options">The <see cref="FileOptions"/> options.</param>
	/// <returns>A <see cref="FileStream"/> opened to the file path specifed.</returns>
	/// <remarks>
	/// The file is opened with ReadWrite access and cannot be opened by another
	/// application until it has been closed.  An IOException is thrown if the
	/// directory specified doesn't exist.
	/// </remarks>
#pragma warning disable IDE0060 // Remove unused parameter
	public static FileStream Create(this IFileSystem fs, string path, int bufferSize, FileOptions options)
#pragma warning restore IDE0060 // Remove unused parameter
		=> new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None, bufferSize, options);


	/// <summary>
	/// Queries the specified <see cref="DirectoryInfo"/> for any files.
	/// </summary>
	/// <param name="directory">The <see cref="DirectoryInfo"/> to query.</param>
	/// <param name="includeChildDirectories">The option to query subdirectories.</param>
	/// <param name="searchPattern">
	/// The optional search string to match against the names of files in path. 
	/// This parameter can contain a combination of valid literal path and
	/// wildcard (* and ?) characters, but it doesn't support regular expressions.
	/// </param>
	/// <param name="take">The optional, number of files to return.</param>
	/// <param name="predicate">The optional predicate, to filter the results.</param>
	/// <returns>The sequence of <see cref="FileInfo"/> objects.</returns>
	public static IEnumerable<FileInfo> QueryFiles(
		this DirectoryInfo directory,
		bool includeChildDirectories = false,
		string? searchPattern = null,
		int take = 0,
		Func<FileInfo, bool>? predicate = null) {
		ArgumentNullException.ThrowIfNull(directory);
		if (!directory.Exists) {
			throw new DirectoryNotFoundException($"Directory not found: '{directory.FullName}'");
		}

		var enumerateOptions = new EnumerationOptions {
			MatchCasing = MatchCasing.CaseInsensitive,
			RecurseSubdirectories = includeChildDirectories
		};

		var patterns = FileSystemUtils.NormalizeSearchPatterns(searchPattern);

		var query = patterns
			.SelectMany(pattern => directory.EnumerateFiles(pattern, enumerateOptions));

		if (predicate != null) {
			query = query.Where(predicate);
		}

		if (take > 0) {
			query = query.Take(take);
		}

		return query;

	}

	/// <summary>
	/// Queries the specified <see cref="DirectoryInfo"/> for any files.
	/// </summary>
	/// <param name="directory">The <see cref="DirectoryInfo"/> to query.</param>
	/// <param name="includeChildDirectories">The option to query subdirectories.</param>
	/// <param name="searchPatterns">
	/// The optional search strings to match against the names of files in path. 
	/// This parameter can contain a combination of valid literal path and
	/// wildcard (* and ?) characters, but it doesn't support regular expressions.
	/// </param>
	/// <param name="take">The optional, number of files to return.</param>
	/// <param name="predicate">The optional predicate, to filter the results.</param>
	/// <returns>The sequence of <see cref="FileInfo"/> objects.</returns>
	/// <remarks>
	/// When multiple search patterns are provided with a <paramref name="take"/> limit, 
	/// the specific items returned may vary between executions due to parallel processing. 
	/// The result count will be consistent, but the exact items selected are non-deterministic.
	/// </remarks>
	public static IEnumerable<FileInfo> QueryFiles(
		this DirectoryInfo directory,
		bool includeChildDirectories = false,
		IEnumerable<string>? searchPatterns = null,
		int take = 0,
		Func<FileInfo, bool>? predicate = null) {
		ArgumentNullException.ThrowIfNull(directory);
		if (!directory.Exists) {
			throw new DirectoryNotFoundException($"Directory not found: '{directory.FullName}'");
		}

		var enumerateOptions = new EnumerationOptions {
			MatchCasing = MatchCasing.CaseInsensitive,
			RecurseSubdirectories = includeChildDirectories
		};

		var patterns = FileSystemUtils.NormalizeSearchPatterns(searchPatterns?.ToArray() ?? []);

		var query = patterns
			.Order()
			.AsParallel()
			.SelectMany(pattern => directory.EnumerateFiles(pattern, enumerateOptions));

		if (predicate != null) {
			query = query.Where(predicate);
		}

		if (take > 0) {
			query = query.Take(take);
		}

		return query;

	}

	/// <summary>
	/// Queries the specified path for any files.
	/// </summary>
	/// <param name="path">The relative or absolute path to the directory to query. This string is not case-sensitive.</param>
	/// <param name="includeChildDirectories">The option to query subdirectories.</param>
	/// <param name="searchPattern">
	/// The optional search string to match against the names of files in path. 
	/// This parameter can contain a combination of valid literal path and
	/// wildcard (* and ?) characters, but it doesn't support regular expressions.
	/// </param>
	/// <param name="take">The optional, number of files to return.</param>
	/// <param name="predicate">The optional predicate, to filter the results.</param>
	/// <returns>The sequence of file paths.</returns>
	public static IEnumerable<string> QueryFiles(
		this string path,
		bool includeChildDirectories = false,
		string? searchPattern = null,
		int take = 0,
		Func<string, bool>? predicate = null) {
		ArgumentException.ThrowIfNullOrWhiteSpace(path);

		if (!Directory.Exists(path)) {
			throw new DirectoryNotFoundException($"Directory not found or path is not a directory: '{path}'");
		}

		var enumerateOptions = new EnumerationOptions {
			MatchCasing = MatchCasing.CaseInsensitive,
			RecurseSubdirectories = includeChildDirectories
		};

		var patterns = FileSystemUtils.NormalizeSearchPatterns(searchPattern);
		var query = patterns
			.SelectMany(pattern => Directory.EnumerateFiles(path, pattern, enumerateOptions));

		if (predicate is not null) {
			query = query.Where(predicate);
		}

		if (take > 0) {
			query = query.Take(take);
		}

		return query;
	}

	/// <summary>
	/// Queries the specified path for any files.
	/// </summary>
	/// <param name="path">The relative or absolute path to the directory to query. This string is not case-sensitive.</param>
	/// <param name="includeChildDirectories">The option to query subdirectories.</param>
	/// <param name="searchPatterns">
	/// An optional array of search strings to match against the names of files in path. 
	/// The search patterns can contain a combination of valid literal path and
	/// wildcard (* and ?) characters, but it doesn't support regular expressions.
	/// </param>
	/// <param name="take">The optional, limit on the number of files to return.</param>
	/// <param name="predicate">The optional predicate, to filter the results.</param>
	/// <returns>The sequence of file paths.</returns>
	/// <remarks>
	/// When multiple search patterns are provided with a <paramref name="take"/> limit, 
	/// the specific items returned may vary between executions due to parallel processing. 
	/// The result count will be consistent, but the exact items selected are non-deterministic.
	/// </remarks>
	public static IEnumerable<string> QueryFiles(
		this string path,
		bool includeChildDirectories = false,
		IEnumerable<string>? searchPatterns = null,
		int take = 0,
		Func<string, bool>? predicate = null) {
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		if (!Directory.Exists(path)) {
			throw new DirectoryNotFoundException($"Directory not found or path is not a directory: '{path}'");
		}

		var patterns = FileSystemUtils.NormalizeSearchPatterns(searchPatterns?.ToArray() ?? []);

		var enumerateOptions = new EnumerationOptions {
			MatchCasing = MatchCasing.CaseInsensitive,
			RecurseSubdirectories = includeChildDirectories
		};

		var query = patterns
			.Order()
			.AsParallel()
			.SelectMany(pattern => Directory.EnumerateFiles(path, pattern, enumerateOptions));

		if (predicate != null) {
			query = query.Where(predicate);
		}

		if (take > 0) {
			query = query.Take(take);
		}

		return query;

	}


	/// <summary>
	/// Attempts to get the requested directories from the specified path.
	/// </summary>
	/// <param name="directory">The <see cref="DirectoryInfo"/> to query.</param>
	/// <param name="includeChildDirectories">The option to search subdirectories.</param>
	/// <param name="searchPattern">
	/// The optional search string to match against the names of directories in path. 
	/// This parameter can contain a combination of valid literal path and
	/// wildcard (* and ?) characters, but it doesn't support regular expressions.
	/// </param>
	/// <param name="take">The optional, number of directories to return.</param>
	/// <param name="predicate">A predicate, to filter the results.</param>
	/// <returns>The sequence of <see cref="DirectoryInfo"/> objects.</returns>
	public static IEnumerable<DirectoryInfo> QueryDirectories(
		this DirectoryInfo directory,
		bool includeChildDirectories = false,
		string? searchPattern = null,
		int take = 0,
		Func<DirectoryInfo, bool>? predicate = null) {
		ArgumentNullException.ThrowIfNull(directory);
		if (!directory.Exists) {
			throw new DirectoryNotFoundException($"Directory not found: '{directory.FullName}'");
		}

		var enumerateOptions = new EnumerationOptions {
			MatchCasing = MatchCasing.CaseInsensitive,
			RecurseSubdirectories = includeChildDirectories
		};

		var patterns = FileSystemUtils.NormalizeSearchPatterns(searchPattern);

		var query = patterns
			.SelectMany(pattern => directory.EnumerateDirectories(pattern, enumerateOptions));

		if (predicate != null) {
			query = query.Where(predicate);
		}

		if (take > 0) {
			query = query.Take(take);
		}

		return query;

	}

	/// <summary>
	/// Attempts to get the requested directories from the specified path.
	/// </summary>
	/// <param name="directory">The <see cref="DirectoryInfo"/> to query.</param>
	/// <param name="includeChildDirectories">The option to search subdirectories.</param>
	/// <param name="searchPatterns">
	/// An optional array of search strings to match against the names of directories in path. 
	/// The search patterns can contain a combination of valid literal path and
	/// wildcard (* and ?) characters, but it doesn't support regular expressions.
	/// </param>
	/// <param name="take">The optional, number of directories to return.</param>
	/// <param name="predicate">A predicate, to filter the results.</param>
	/// <returns>The sequence of <see cref="DirectoryInfo"/> objects.</returns>
	/// <remarks>
	/// When multiple search patterns are provided with a <paramref name="take"/> limit, 
	/// the specific items returned may vary between executions due to parallel processing. 
	/// The result count will be consistent, but the exact items selected are non-deterministic.
	/// </remarks>
	public static IEnumerable<DirectoryInfo> QueryDirectories(
		this DirectoryInfo directory,
		bool includeChildDirectories = false,
		IEnumerable<string>? searchPatterns = null,
		int take = 0,
		Func<DirectoryInfo, bool>? predicate = null) {
		ArgumentNullException.ThrowIfNull(directory);
		if (!directory.Exists) {
			throw new DirectoryNotFoundException($"Directory not found: '{directory.FullName}'");
		}

		var enumerateOptions = new EnumerationOptions {
			MatchCasing = MatchCasing.CaseInsensitive,
			RecurseSubdirectories = includeChildDirectories
		};

		var patterns = FileSystemUtils.NormalizeSearchPatterns(searchPatterns?.ToArray() ?? []);

		var query = patterns
			.Order()
			.AsParallel()
			.SelectMany(pattern => directory.EnumerateDirectories(pattern, enumerateOptions));

		if (predicate != null) {
			query = query.Where(predicate);
		}

		if (take > 0) {
			query = query.Take(take);
		}

		return query;

	}

	/// <summary>
	/// Attempts to get the requested directories from the specified path.
	/// </summary>
	/// <param name="path">The path to the directory.</param>
	/// <param name="includeChildDirectories">The option to search subdirectories.</param>
	/// <param name="searchPattern">
	/// The optional search string to match against the names of directories in path. 
	/// This parameter can contain a combination of valid literal path and
	/// wildcard (* and ?) characters, but it doesn't support regular expressions.
	/// </param>
	/// <param name="take">The optional, number of directories to return.</param>
	/// <param name="predicate">The optional predicate, to filter the results.</param>
	/// <returns>The sequence of directory paths.</returns>
	public static IEnumerable<string> QueryDirectories(
		this string path,
		bool includeChildDirectories = false,
		string? searchPattern = null,
		int take = 0,
		Func<string, bool>? predicate = null) {
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		if (!Directory.Exists(path)) {
			throw new DirectoryNotFoundException($"Directory not found or path is not a directory: '{path}'");
		}

		var enumerateOptions = new EnumerationOptions {
			MatchCasing = MatchCasing.CaseInsensitive,
			RecurseSubdirectories = includeChildDirectories
		};

		var patterns = FileSystemUtils.NormalizeSearchPatterns(searchPattern);

		var query = patterns
			.SelectMany(pattern => Directory.EnumerateDirectories(path, pattern, enumerateOptions));

		if (predicate != null) {
			query = query.Where(predicate);
		}

		if (take > 0) {
			query = query.Take(take);
		}

		return query;

	}

	/// <summary>
	/// Attempts to get the requested directories from the specified path.
	/// </summary>
	/// <param name="path">The path to the directory.</param>
	/// <param name="includeChildDirectories">The option to search subdirectories.</param>
	/// <param name="searchPatterns">
	/// An optional array of search strings to match against the names of directories in path. 
	/// The search patterns can contain a combination of valid literal path and
	/// wildcard (* and ?) characters, but it doesn't support regular expressions.
	/// </param>
	/// <param name="take">The optional, number of directories to return.</param>
	/// <param name="predicate">The optional predicate, to filter the results.</param>
	/// <returns>The sequence of directory paths.</returns>
	/// <remarks>
	/// When multiple search patterns are provided with a <paramref name="take"/> limit, 
	/// the specific items returned may vary between executions due to parallel processing. 
	/// The result count will be consistent, but the exact items selected are non-deterministic.
	/// </remarks>
	public static IEnumerable<string> QueryDirectories(
		this string path,
		bool includeChildDirectories = false,
		IEnumerable<string>? searchPatterns = null,
		int take = 0,
		Func<string, bool>? predicate = null) {
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		if (!Directory.Exists(path)) {
			throw new DirectoryNotFoundException($"Directory not found or path is not a directory: '{path}'");
		}

		var enumerateOptions = new EnumerationOptions {
			MatchCasing = MatchCasing.CaseInsensitive,
			RecurseSubdirectories = includeChildDirectories
		};

		var patterns = FileSystemUtils.NormalizeSearchPatterns(searchPatterns?.ToArray() ?? []);

		var query = patterns
			.Order()
			.AsParallel()
			.SelectMany(pattern => Directory.EnumerateDirectories(path, pattern, enumerateOptions));

		if (predicate != null) {
			query = query.Where(predicate);
		}

		if (take > 0) {
			query = query.Take(take);
		}

		return query;

	}


}