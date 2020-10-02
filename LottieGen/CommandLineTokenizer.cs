// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

// Recognizes command line argument keywords such as "-help" or "-include". Matches partial
// strings as long as they uniquely identify the keyword. Matching is case insenstive.
sealed class CommandlineTokenizer<TKeywordId>
    where TKeywordId : struct, IComparable
{
    readonly TrieNode _root = new TrieNode('\0', default);
    readonly TKeywordId _ambiguousValue;

    internal CommandlineTokenizer(TKeywordId ambiguousValue)
    {
        _ambiguousValue = ambiguousValue;
    }

    internal CommandlineTokenizer<TKeywordId> AddPrefixedKeyword(TKeywordId id) =>
        AddPrefixedKeyword(id, Enum.GetName(typeof(TKeywordId), id)!);

    internal CommandlineTokenizer<TKeywordId> AddPrefixedKeyword(TKeywordId id, string keyword)
    {
        AddKeyword(id, $"-{keyword}");
        return AddKeyword(id, $"/{keyword}");
    }

    internal CommandlineTokenizer<TKeywordId> AddKeyword(TKeywordId id) =>
        AddKeyword(id, Enum.GetName(typeof(TKeywordId), id)!);

    // Add a keyword to the recognizer.
    internal CommandlineTokenizer<TKeywordId> AddKeyword(TKeywordId id, string keyword)
    {
        if (IsAmbiguousId(id) || IsDefaultId(id))
        {
            throw new ArgumentException();
        }

        TrieNode currrentNode = _root;

        foreach (var ch in keyword)
        {
            // Search through the current set of nodes to see if there is a node
            // for the current character already. If there is, use it, if not, add one.
            bool matched = false;
            foreach (var node in currrentNode.Children)
            {
                if (node.Matches(ch))
                {
                    // There is already a node that matches this character.
                    matched = true;
                    currrentNode = node;

                    if (node.IsKeywordMatch(id))
                    {
                        throw new ArgumentException("Keyword already added.");
                    }

                    // If the node is a terminal (i.e. it has no children) then this keyword
                    // cannot be added as it is the same as an existing keyword.
                    if (node.Children.Count == 0)
                    {
                        // No children, therefore this node is a terminal node.
                        throw new ArgumentException("Keyword redefinition.");
                    }
                    else
                    {
                        // Not a terminal node. Indicate that this node matches more than
                        // one keyword.
                        node.Keyword = _ambiguousValue;
                    }

                    break;
                }
            }

            // If no matching node found, add a new node, and set the Keyword to indicate
            // that a match up to here matches the keyword being added.
            if (!matched)
            {
                var newNode = new TrieNode(ch, id);
                currrentNode.Children.Add(newNode);
                currrentNode = newNode;
            }
        }

        if (currrentNode.IsKeywordMatch(_ambiguousValue))
        {
            throw new ArgumentException("Keyword matches an existing prefix.");
        }

        return this;
    }

    internal IEnumerable<(TKeywordId, string)> Tokenize(string[] args)
    {
        foreach (var arg in args)
        {
            TryMatchKeyword(arg, out var keywordId);
            yield return (keywordId, arg);
        }
    }

    // Attempts to match the keyword to one that has been added to the recognizer. On success
    // return true and sets the id to the matching keyword. On failure returns false
    // and sets the id to default if no part of the keyword matched or to the ambigous value
    // if the match was ambiguous.
    internal bool TryMatchKeyword(string keyword, out TKeywordId id)
    {
        if (keyword.Length == 0)
        {
            id = default;
            return false;
        }

        var currentNode = _root;

        foreach (var ch in keyword)
        {
            bool matched = false;

            // Do a linear search of the child nodes. Linear search is appropriate
            // because typically there are very few children.
            foreach (var node in currentNode.Children)
            {
                if (node.Matches(ch))
                {
                    matched = true;
                    currentNode = node;
                    break;
                }
            }

            // If no child matched, return false.
            if (!matched)
            {
                id = default;
                return false;
            }
        }

        // The whole of the given keyword matched.
        id = currentNode.Keyword;
        return !IsAmbiguousId(id);
    }

    static bool IsEqual(TKeywordId a, TKeywordId b) => a.CompareTo(b) == 0;

    static bool IsDefaultId(TKeywordId value) => IsEqual(value, default);

    bool IsAmbiguousId(TKeywordId value) => IsEqual(value, _ambiguousValue);

    /// <summary>
    /// A node in the trie.
    /// </summary>
    sealed class TrieNode
    {
        readonly char _character;

        internal TrieNode(char character, TKeywordId keywordId)
        {
            _character = char.ToLower(character);
            Keyword = keywordId;
        }

        internal TKeywordId Keyword { get; set; }

        internal bool IsKeywordMatch(TKeywordId keywordId) => IsEqual(Keyword, keywordId);

        internal List<TrieNode> Children { get; } = new List<TrieNode>();

        internal bool Matches(char character) => char.ToLower(character) == _character;

        public override string ToString() => string.Join(", ", GetStrings());

        IEnumerable<string> GetStrings()
        {
            var ch = _character == '\0' ? string.Empty : _character.ToString();
            if (Children.Count == 0)
            {
                yield return ch;
            }
            else
            {
                foreach (var child in Children)
                {
                    foreach (var childString in child.GetStrings())
                    {
                        yield return $"{ch}{childString}";
                    }
                }
            }
        }
    }
}
