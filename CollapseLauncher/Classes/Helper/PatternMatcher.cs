using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CollapseLauncher.Helper
{
    public static class PatternMatcher
    {
        /// <summary>
        /// Determines whether the specified input string matches the given pattern.
        /// The pattern can contain wildcards ('*').
        /// </summary>
        /// <param name="input">The input string to match.</param>
        /// <param name="pattern">The pattern to match against, which can contain wildcards ('*').</param>
        /// <returns>True if the input matches the pattern; otherwise, false.</returns>
        public static bool MatchSimpleExpression(string input, string pattern)
        {
            // Escape special regex characters in the pattern except for the wildcard '*'
            var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
            return Regex.IsMatch(input, regexPattern,
                                 RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.NonBacktracking);
        }

        /// <summary>
        /// Determines whether the specified input string matches any pattern in the given list of patterns.
        /// </summary>
        /// <param name="input">The input string to match.</param>
        /// <param name="patterns">A list of patterns to match against, each of which can contain wildcards ('*').</param>
        /// <returns>True if the input matches any pattern in the list; otherwise, false.</returns>
        public static bool MatchesAnyPattern(string input, List<string> patterns)
        {
            return patterns.Any(pattern => MatchSimpleExpression(input, pattern));
        }
    }
}