namespace Cirreum.FileSystem;

using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Retry;
using System;
using System.IO;
using System.IO.Compression;

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

	private static readonly IEnumerable<TimeSpan> DeleteFileBackoff =
		Backoff.DecorrelatedJitterBackoffV2(TimeSpan.FromMilliseconds(DeleteFileDelayMs), MaxRetryAttempts);
	private static readonly RetryPolicy DeleteFilePolicy =
		Policy.Handle<IOException>()
		.Or<UnauthorizedAccessException>()
		.WaitAndRetry(DeleteFileBackoff);


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

	public void DeleteFile(string path) {
		ArgumentException.ThrowIfNullOrWhiteSpace(path);

		File.Delete(path);

	}

	public void DeleteFileWithRetry(string path) {
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		DeleteFilePolicy.Execute(() => {
			File.Delete(path);
		});
	}

	public void DeleteDirectory(string path, bool recursive) {
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		if (!Directory.Exists(path)) {
			return; // Already deleted, idempotent
		}
		Directory.Delete(path, recursive);

	}

	public void DeleteChildDirectories(string rootPath) {
		ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

		var dirs = Directory.EnumerateDirectories(rootPath);
		foreach (var dir in dirs) {
			Directory.Delete(dir, true);
		}

	}

	public void MoveFile(string sourceFileName, string destFileName, bool overwrite = false) {
		ArgumentException.ThrowIfNullOrWhiteSpace(sourceFileName);
		ArgumentException.ThrowIfNullOrWhiteSpace(destFileName);

		File.Move(sourceFileName, destFileName, overwrite);

	}

	public void MoveDirectory(string sourceDirName, string destDirName) {
		ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirName);
		ArgumentException.ThrowIfNullOrWhiteSpace(destDirName);

		Directory.Move(sourceDirName, destDirName);

	}

	public void CopyFile(string sourceFileName, string destFileName, bool overwrite = false) {
		ArgumentException.ThrowIfNullOrWhiteSpace(sourceFileName);
		ArgumentException.ThrowIfNullOrWhiteSpace(destFileName);

		File.Copy(sourceFileName, destFileName, overwrite);

	}

	public void CopyDirectory(string sourceDirName, string destDirName, bool copySubDirs, bool overwrite = false) {
		ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirName);
		ArgumentException.ThrowIfNullOrWhiteSpace(destDirName);

		var sourceDir = new DirectoryInfo(sourceDirName);
		if (!sourceDir.Exists) {
			throw new DirectoryNotFoundException($"Source directory not found: '{sourceDirName}'");
		}

		CopyDirectoryImpl(sourceDir, destDirName, copySubDirs, overwrite);
	}
	private static void CopyDirectoryImpl(DirectoryInfo sourceDir, string destDirName, bool copySubDirs, bool overwrite) {
		//
		// Ensure destination exists
		//
		if (Directory.Exists(destDirName) && !overwrite) {
			throw new IOException($"Destination directory already exists: '{destDirName}'");
		}
		//
		// Ensure destination exists
		//
		Directory.CreateDirectory(destDirName);

		//
		// Copy the files
		//
		sourceDir.EnumerateFiles().AsParallel().ForAll(file => {
			var newFile = Path.Combine(destDirName, file.Name);
			file.CopyTo(newFile, overwrite);
		});

		//
		// Optionally, recurse on sub directories.
		//
		if (copySubDirs) {

			sourceDir.EnumerateDirectories().AsParallel().ForAll(subDir => {
				var newDir = Path.Combine(destDirName, subDir.Name);
				CopyDirectoryImpl(subDir, newDir, copySubDirs, overwrite);
			});

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