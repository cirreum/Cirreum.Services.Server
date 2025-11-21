namespace Cirreum.FileSystem;

using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Retry;
using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;

sealed class LocalFileSystem : IFileSystem {

	private const int MaxRetryAttempts = 3;
	private const int DeleteDirDelayMs = 1000;
	private const int DeleteFileDelayMs = 1500;

	private static readonly IEnumerable<TimeSpan> DeleteDirBackoff =
		Backoff.DecorrelatedJitterBackoffV2(TimeSpan.FromMilliseconds(DeleteDirDelayMs), MaxRetryAttempts);
	private static readonly RetryPolicy DeleteDirPolicy =
		Policy.Handle<IOException>()
		.Or<DirectoryNotFoundException>()
		.WaitAndRetry(DeleteDirBackoff);
	private static readonly AsyncRetryPolicy DeleteDirAsyncPolicy =
		Policy.Handle<IOException>()
		.Or<DirectoryNotFoundException>()
		.WaitAndRetryAsync(DeleteDirBackoff);

	private static readonly IEnumerable<TimeSpan> DeleteFileBackoff =
		Backoff.DecorrelatedJitterBackoffV2(TimeSpan.FromMilliseconds(DeleteFileDelayMs), MaxRetryAttempts);
	private static readonly RetryPolicy DeleteFilePolicy =
		Policy.Handle<IOException>()
		.Or<UnauthorizedAccessException>()
		.WaitAndRetry(DeleteFileBackoff);
	private static readonly AsyncRetryPolicy DeleteFileAsyncPolicy =
		Policy.Handle<IOException>()
		.Or<UnauthorizedAccessException>()
		.WaitAndRetryAsync(DeleteFileBackoff);


	public bool FileExists(string path) {
		return File.Exists(path);
	}

	public bool DirectoryExists(string path) {
		return Directory.Exists(path);
	}

	public bool EnsureDirectory(string path) {
		ArgumentException.ThrowIfNullOrWhiteSpace(path);

		if (Directory.Exists(path)) {
			return true;
		}

		var dirInfo = Directory.CreateDirectory(path);
		dirInfo.Refresh();

		return dirInfo.Exists;

	}

	public string[] GetFiles(string path, string searchPattern, bool includeChildDirectories) {
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		if (!Directory.Exists(path)) {
			throw new DirectoryNotFoundException($"Directory not found or path is not a directory: '{path}'");
		}

		var result = Array.Empty<string>();

		var pattern = FileSystemUtils.NormalizeSearchPatterns(searchPattern).First();

		if (path.GetPathType() == PathType.Directory) {
			var options = new EnumerationOptions {
				MatchCasing = MatchCasing.CaseInsensitive,
				RecurseSubdirectories = includeChildDirectories
			};
			result = Directory.GetFiles(path, pattern, options);
		}

		return result;

	}


	public IEnumerable<string> QueryFiles(string[] paths, bool includeChildDirectories, string searchPattern, Func<string, bool>? predicate = null, int take = 0) {
		ArgumentNullException.ThrowIfNull(paths);
		var counter = 0;
		var hasLimit = take > 0;

		for (var i = 0; i < paths.Length; i++) {
			if (hasLimit && counter >= take) {
				break;
			}

			var path = paths[i];
			if (string.IsNullOrWhiteSpace(path)) {
				continue;
			}

			var limit = hasLimit ? take - counter : 0;
			foreach (var f in path.QueryFiles(includeChildDirectories, searchPattern, limit, predicate)) {
				counter++;
				yield return f;
			}
		}
	}
	public IEnumerable<string> QueryFiles(string path, bool includeChildDirectories, string searchPattern, Func<string, bool>? predicate = null, int take = 0) {
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		return path.QueryFiles(includeChildDirectories, searchPattern, take, predicate);
	}
	public IEnumerable<string> QueryFiles(string path, bool includeChildDirectories, IEnumerable<string> searchPatterns, Func<string, bool>? predicate = null, int take = 0) {
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		return path.QueryFiles(includeChildDirectories, searchPatterns, take, predicate);
	}

	public async IAsyncEnumerable<string> QueryFilesAsync(
		string[] paths,
		bool includeChildDirectories,
		string searchPattern,
		Func<string, bool>? predicate = null,
		int take = 0,
		[EnumeratorCancellation] CancellationToken cancellationToken = default) {

		var count = 0;
		foreach (var path in paths) {
			await foreach (var file in this.QueryFilesAsync(path, includeChildDirectories, searchPattern, predicate, take > 0 ? take - count : 0, cancellationToken)) {
				yield return file;
				if (take > 0 && ++count >= take) {
					yield break;
				}
			}
		}
	}
	public async IAsyncEnumerable<string> QueryFilesAsync(
			string path,
			bool includeChildDirectories,
			string searchPattern,
			Func<string, bool>? predicate = null,
			int take = 0,
			[EnumeratorCancellation] CancellationToken cancellationToken = default) {

		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		ArgumentException.ThrowIfNullOrWhiteSpace(searchPattern);

		var options = new EnumerationOptions {
			RecurseSubdirectories = includeChildDirectories,
			IgnoreInaccessible = true,
			ReturnSpecialDirectories = false
		};

		var count = 0;

		foreach (var file in Directory.EnumerateFiles(path, searchPattern, options)) {
			cancellationToken.ThrowIfCancellationRequested();

			if (predicate == null || predicate(file)) {
				yield return file;
				if (take > 0 && ++count >= take) {
					yield break;
				}
			}
		}
	}
	public async IAsyncEnumerable<string> QueryFilesAsync(
		string path,
		bool includeChildDirectories,
		IEnumerable<string> searchPatterns,
		Func<string, bool>? predicate = null,
		int take = 0,
		[EnumeratorCancellation] CancellationToken cancellationToken = default) {

		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		ArgumentNullException.ThrowIfNull(searchPatterns);

		var count = 0;
		foreach (var pattern in searchPatterns) {
			await foreach (var file in this.QueryFilesAsync(path, includeChildDirectories, pattern, predicate, take > 0 ? take - count : 0, cancellationToken)) {
				yield return file;
				if (take > 0 && ++count >= take) {
					yield break;
				}
			}
		}
	}



	public IEnumerable<string> QueryDirectories(string[] paths, bool includeChildDirectories, string searchPattern, Func<string, bool>? predicate = null, int take = 0) {
		ArgumentNullException.ThrowIfNull(paths);
		var counter = 0;
		var hasLimit = take > 0;

		for (var i = 0; i < paths.Length; i++) {
			if (hasLimit && counter >= take) {
				break;
			}

			var path = paths[i];
			if (string.IsNullOrWhiteSpace(path)) {
				continue;
			}

			var limit = hasLimit ? take - counter : 0;
			foreach (var d in path.QueryDirectories(includeChildDirectories, searchPattern, limit, predicate)) {
				counter++;
				yield return d;
			}
		}
	}
	public IEnumerable<string> QueryDirectories(string path, bool includeChildDirectories, string searchPattern, Func<string, bool>? predicate = null, int take = 0) {
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		return path.QueryDirectories(includeChildDirectories, searchPattern, take, predicate);
	}
	public IEnumerable<string> QueryDirectories(string path, bool includeChildDirectories, IEnumerable<string> searchPatterns, Func<string, bool>? predicate = null, int take = 0) {
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		return path.QueryDirectories(includeChildDirectories, searchPatterns, take, predicate);
	}

	public async IAsyncEnumerable<string> QueryDirectoriesAsync(
		string[] paths,
		bool includeChildDirectories,
		string searchPattern,
		Func<string, bool>? predicate = null,
		int take = 0,
		[EnumeratorCancellation] CancellationToken cancellationToken = default) {

		var count = 0;
		foreach (var path in paths) {
			await foreach (var directory in this.QueryDirectoriesAsync(path, includeChildDirectories, searchPattern, predicate, take > 0 ? take - count : 0, cancellationToken)) {
				yield return directory;
				if (take > 0 && ++count >= take) {
					yield break;
				}
			}
		}
	}

	public async IAsyncEnumerable<string> QueryDirectoriesAsync(
		string path,
		bool includeChildDirectories,
		string searchPattern,
		Func<string, bool>? predicate = null,
		int take = 0,
		[EnumeratorCancellation] CancellationToken cancellationToken = default) {

		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		ArgumentException.ThrowIfNullOrWhiteSpace(searchPattern);

		var options = new EnumerationOptions {
			RecurseSubdirectories = includeChildDirectories,
			IgnoreInaccessible = true,
			ReturnSpecialDirectories = false
		};

		var count = 0;

		foreach (var directory in Directory.EnumerateDirectories(path, searchPattern, options)) {
			cancellationToken.ThrowIfCancellationRequested();

			if (predicate == null || predicate(directory)) {
				yield return directory;
				if (take > 0 && ++count >= take) {
					yield break;
				}
			}
		}
	}

	public async IAsyncEnumerable<string> QueryDirectoriesAsync(
		string path,
		bool includeChildDirectories,
		IEnumerable<string> searchPatterns,
		Func<string, bool>? predicate = null,
		int take = 0,
		[EnumeratorCancellation] CancellationToken cancellationToken = default) {

		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		ArgumentNullException.ThrowIfNull(searchPatterns);

		var count = 0;
		foreach (var pattern in searchPatterns) {
			await foreach (var directory in this.QueryDirectoriesAsync(path, includeChildDirectories, pattern, predicate, take > 0 ? take - count : 0, cancellationToken)) {
				yield return directory;
				if (take > 0 && ++count >= take) {
					yield break;
				}
			}
		}
	}


	public void ExtractZipFile(string source, string destination, bool overwriteFiles) {
		ArgumentException.ThrowIfNullOrWhiteSpace(source);
		ArgumentException.ThrowIfNullOrWhiteSpace(destination);

		if (!source.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) {
			throw new ArgumentException("Source file must be a .zip file.", nameof(source));
		}

		if (!File.Exists(source)) {
			throw new FileNotFoundException("Source zip file not found.", source);
		}

		var directoryCreated = false;
		if (!Directory.Exists(destination)) {
			directoryCreated = true;
			Directory.CreateDirectory(destination);
		}

		try {
			ZipFile.ExtractToDirectory(source, destination, overwriteFiles);
		} catch {
			if (directoryCreated) {
				DeleteDirPolicy.ExecuteAndCapture(() => Directory.Delete(destination, true));
			}
			throw; // Re-throw the original exception
		}
	}
	public async Task ExtractZipFileAsync(string source, string destination, bool overwriteFiles, CancellationToken cancellationToken = default) {
		ArgumentException.ThrowIfNullOrWhiteSpace(source);
		ArgumentException.ThrowIfNullOrWhiteSpace(destination);

		if (!source.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) {
			throw new ArgumentException("Source file must be a .zip file.", nameof(source));
		}

		if (!File.Exists(source)) {
			throw new FileNotFoundException("Source zip file not found.", source);
		}

		var directoryCreated = false;
		if (!Directory.Exists(destination)) {
			directoryCreated = true;
			Directory.CreateDirectory(destination);
		}

		try {
			await ZipFile.ExtractToDirectoryAsync(source, destination, overwriteFiles, cancellationToken);
		} catch {
			if (directoryCreated) {
				DeleteDirPolicy.ExecuteAndCapture(() => Directory.Delete(destination, true));
			}
			throw; // Re-throw the original exception
		}
	}

	public void DeleteFile(string path) {
		ArgumentException.ThrowIfNullOrWhiteSpace(path);

		File.Delete(path);

	}
	public Task DeleteFileAsync(string path, CancellationToken cancellationToken = default) {
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		return Task.Run(() => File.Delete(path), cancellationToken);
	}

	public void DeleteFileWithRetry(string path) {
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		DeleteFilePolicy.Execute(() => {
			File.Delete(path);
		});
	}
	public Task DeleteFileWithRetryAsync(string path, CancellationToken cancellationToken = default) {
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		return DeleteFileAsyncPolicy.ExecuteAsync(() => {
			File.Delete(path);
			return Task.CompletedTask;
		});
	}

	public void DeleteDirectory(string path, bool recursive) {
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		if (!Directory.Exists(path)) {
			return; // Already deleted, idempotent
		}
		Directory.Delete(path, recursive);

	}
	public Task DeleteDirectoryAsync(string path, bool recursive, CancellationToken cancellationToken = default) {
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		return DeleteFileAsyncPolicy.ExecuteAsync(() => {
			if (!Directory.Exists(path)) {
				return Task.CompletedTask; // Already deleted, idempotent
			}
			Directory.Delete(path, recursive);
			return Task.CompletedTask;
		});
	}

	public void DeleteChildDirectories(string rootPath) {
		ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
		var dirs = Directory.EnumerateDirectories(rootPath);
		foreach (var dir in dirs) {
			Directory.Delete(dir, true);
		}
	}
	public Task DeleteChildDirectoriesAsync(string rootPath, CancellationToken cancellationToken = default) {
		ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
		return Task.Run(() => {
			var dirs = Directory.EnumerateDirectories(rootPath);
			foreach (var dir in dirs) {
				cancellationToken.ThrowIfCancellationRequested();
				Directory.Delete(dir, true);
			}
		}, cancellationToken);
	}

	public void MoveFile(string sourceFileName, string destFileName, bool overwrite = false) {
		ArgumentException.ThrowIfNullOrWhiteSpace(sourceFileName);
		ArgumentException.ThrowIfNullOrWhiteSpace(destFileName);

		File.Move(sourceFileName, destFileName, overwrite);

	}
	public Task MoveFileAsync(string sourceFileName, string destFileName, bool overwrite = false, CancellationToken cancellationToken = default) {
		ArgumentException.ThrowIfNullOrWhiteSpace(sourceFileName);
		ArgumentException.ThrowIfNullOrWhiteSpace(destFileName);
		return Task.Run(() => File.Move(sourceFileName, destFileName, overwrite), cancellationToken);
	}

	public void MoveDirectory(string sourceDirName, string destDirName) {
		ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirName);
		ArgumentException.ThrowIfNullOrWhiteSpace(destDirName);

		Directory.Move(sourceDirName, destDirName);

	}
	public Task MoveDirectoryAsync(string sourceDirName, string destDirName, CancellationToken cancellationToken = default) {
		ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirName);
		ArgumentException.ThrowIfNullOrWhiteSpace(destDirName);
		return Task.Run(() => Directory.Move(sourceDirName, destDirName), cancellationToken);
	}

	public void CopyFile(string sourceFileName, string destFileName, bool overwrite = false) {
		ArgumentException.ThrowIfNullOrWhiteSpace(sourceFileName);
		ArgumentException.ThrowIfNullOrWhiteSpace(destFileName);

		File.Copy(sourceFileName, destFileName, overwrite);

	}
	public Task CopyFileAsync(string sourceFileName, string destFileName, bool overwrite = false, CancellationToken cancellationToken = default) {
		ArgumentException.ThrowIfNullOrWhiteSpace(sourceFileName);
		ArgumentException.ThrowIfNullOrWhiteSpace(destFileName);
		return Task.Run(() => File.Copy(sourceFileName, destFileName, overwrite), cancellationToken);
	}

	public void CopyDirectory(string sourceDirName, string destDirName, bool copySubDirs, bool overwrite = false) {
		ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirName);
		ArgumentException.ThrowIfNullOrWhiteSpace(destDirName);

		var sourceDir = new DirectoryInfo(sourceDirName);
		if (!sourceDir.Exists) {
			throw new DirectoryNotFoundException($"Source directory not found: '{sourceDirName}'");
		}

		CopyDirectoryImpl(sourceDir, destDirName, copySubDirs, overwrite, CancellationToken.None);
	}
	public Task CopyDirectoryAsync(string sourceDirName, string destDirName, bool copySubDirs, bool overwrite = false, CancellationToken cancellationToken = default) {
		ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirName);
		ArgumentException.ThrowIfNullOrWhiteSpace(destDirName);

		return Task.Run(() => {
			var sourceDir = new DirectoryInfo(sourceDirName);
			if (!sourceDir.Exists) {
				throw new DirectoryNotFoundException($"Source directory not found: '{sourceDirName}'");
			}
			CopyDirectoryImpl(sourceDir, destDirName, copySubDirs, overwrite, cancellationToken);
		}, cancellationToken);
	}
	private static void CopyDirectoryImpl(DirectoryInfo sourceDir, string destDirName, bool copySubDirs, bool overwrite, CancellationToken cancellationToken) {
		cancellationToken.ThrowIfCancellationRequested();

		//
		// Ensure destination exists
		//
		if (Directory.Exists(destDirName) && !overwrite) {
			throw new IOException($"Destination directory already exists: '{destDirName}'");
		}

		//
		// Create destination directory
		//
		Directory.CreateDirectory(destDirName);

		//
		// Copy the files
		//
		foreach (var file in sourceDir.EnumerateFiles()) {
			cancellationToken.ThrowIfCancellationRequested();
			var newFile = Path.Combine(destDirName, file.Name);
			file.CopyTo(newFile, overwrite);
		}

		//
		// Optionally, recurse on sub directories.
		//
		if (copySubDirs) {
			foreach (var subDir in sourceDir.EnumerateDirectories()) {
				cancellationToken.ThrowIfCancellationRequested();
				var newDir = Path.Combine(destDirName, subDir.Name);
				CopyDirectoryImpl(subDir, newDir, copySubDirs, overwrite, cancellationToken);
			}
		}
	}

	public void WriteAllText(string path, string contents) {
		File.WriteAllText(path, contents);
	}
	public Task WriteAllTextAsync(string path, string contents, CancellationToken cancellationToken = default) {
		return File.WriteAllTextAsync(path, contents, cancellationToken);
	}

	public string ReadAllText(string path) {
		return File.ReadAllText(path);
	}
	public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default) {
		return File.ReadAllTextAsync(path, cancellationToken);
	}

}