// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

static class Glob
{
    /// <summary>
    /// Returns the files that match the given pattern.
    /// </summary>
    /// <returns>A list of pairs identifying the path to each file, and a path relative to the deepest
    /// non-wildcarded part of the input.</returns>
    internal static IEnumerable<(string path, string relativePath)> EnumerateFiles(string value)
    {
        // Break the pattern into non-wildcarded directory and the pattern to match.
        var (directory, pattern) = SplitPath(value);

        // Find all the matching files.
        foreach (var file in EnumerateFilesInternal(directory, pattern))
        {
            // Get the full path of the file.
            var filePath = Path.GetFullPath(file);

            // Get just the filename part.
            var fileName = Path.GetFileName(filePath);

            // Get the path of the directory where the file is, relative to the
            // non-wildcarded part of the input.
            var relativePath = filePath.Substring(directory.Length, filePath.Length - fileName.Length - directory.Length);

            yield return (filePath, relativePath);
        }

        yield break;
    }

    static IEnumerable<string> EnumerateFilesInternal(string directory, string[] pattern)
    {
        if (!Directory.Exists(directory))
        {
            yield break;
        }

        // If the pattern is a leaf and contains no globstars, just enumerate the files.
        if (pattern.Length == 1 && !ContainsGlobstars(pattern[0]))
        {
            foreach (var filename in Directory.EnumerateFiles(directory, pattern[0]))
            {
                yield return filename;
            }

            yield break;
        }

        // If the first segment has no wildcards, just combine with the directory and recurse.
        if (pattern.Length > 1 && !ContainsWildcards(pattern[0]))
        {
            foreach (var file in EnumerateFilesInternal(Path.Combine(directory, pattern[0]), pattern.Skip(1).ToArray()))
            {
                yield return file;
            }

            yield break;
        }

        // There are globstars and/or directory wildcards.
        if (!ContainsGlobstars(pattern[0]))
        {
            // No globstars in the first segment. Enumerate the directories and recurse.
            foreach (var subdirectory in Directory.EnumerateDirectories(directory, pattern[0]))
            {
                foreach (var file in EnumerateFilesInternal(subdirectory, pattern.Skip(1).ToArray()))
                {
                    yield return file;
                }
            }

            yield break;
        }

        // Search everything under the directory and return only those that
        // match the pattern.
        var wildcardRegex = WildcardToRegex(string.Join(Path.DirectorySeparatorChar, pattern));

        foreach (var file in Directory.EnumerateFiles(
            directory,
            "*",
            new EnumerationOptions { RecurseSubdirectories = true }))
        {
            // Only return the path if it matches the wildcard.
            var wildcardedPart = file.Substring(directory.Length);
            if (wildcardRegex.Match(wildcardedPart).Success)
            {
                yield return file;
            }
        }
    }

    static Regex WildcardToRegex(string wildcard)
    {
        // Escape the wildcard so it can be used in a regex.
        var regex = Regex.Escape(wildcard);

        // Match **. Matches 0 or more, including \.
        regex = Regex.Replace(regex, @"\\\*\\\*(\\\*)*", @".*");

        // Match *. Matches 0 or more except \.
        regex = Regex.Replace(regex, @"\\\*", @"[^\\]*");

        // Match ?. Matches 1 except \.
        regex = Regex.Replace(regex, @"\\\?", @"[^\\]");

        // Make the regex only match at the end of the string.
        regex += "$";

        return new Regex(regex, RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Splits a path into a non-wildcarded directory path and
    /// directory segments where the first segment is a leaf or
    /// contains wildcards. The returned directory path will
    /// end with a forward slash.
    /// </summary>
    static (string directoryPath, string[] segments) SplitPath(string path)
    {
        var pathRoot = Path.GetPathRoot(path);
        var pathWithoutRoot = path.Substring(pathRoot.Length);

        var segments = pathWithoutRoot.Split(new char[] { Path.DirectorySeparatorChar });

        var directoryPath = pathRoot;
        var pattern = segments;

        if (segments.Length > 1)
        {
            // There is more than one segment.
            // Assume no wildcards in any directory segments.
            directoryPath = pathRoot + string.Join(Path.DirectorySeparatorChar, segments.Take(segments.Length - 1));
            pattern = new[] { segments[segments.Length - 1] };

            // There's a directory separator. The directory path is all the segments
            // up to the first segment that has a wildcard.
            for (var i = 0; i < segments.Length - 1; i++)
            {
                if (ContainsWildcards(segments[i]))
                {
                    // Found a segment with wildcards.
                    directoryPath = pathRoot + string.Join(Path.DirectorySeparatorChar, segments.Take(i));
                    pattern = segments.Skip(i).ToArray();
                    break;
                }
            }
        }

        if (string.IsNullOrEmpty(directoryPath))
        {
            directoryPath = @".\";
        }

        if (directoryPath.EndsWith(":"))
        {
            // The directory path is just a drive letter with no directory.
            // Ensure it is treated as the current directory on the drive.
            directoryPath += @".\";
        }

        // Ensure that we always have a trailing backslash. Callers expect this.
        if (!directoryPath.EndsWith(@"\"))
        {
            directoryPath += @"\";
        }

        return (Path.GetFullPath(directoryPath), pattern);
    }

    static bool ContainsWildcards(string pattern)
    {
        return pattern.IndexOfAny(new[] { '*', '?' }) >= 0;
    }

    static bool ContainsGlobstars(string pattern)
    {
        return pattern.IndexOf("**") >= 0;
    }

    static bool ContainsPathSeparators(string pattern)
    {
        return pattern.IndexOf(Path.DirectorySeparatorChar) >= 0;
    }
}
