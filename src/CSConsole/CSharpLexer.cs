﻿using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityExplorer.CSConsole.Lexer;

namespace UnityExplorer.CSConsole
{
    public struct LexerMatchInfo
    {
        public int startIndex;
        public int endIndex;
        public string htmlColor;
    }

    public enum DelimiterType
    {
        Start,
        End,
    };

    public class CSharpLexer
    {
        private string inputString;
        private readonly Matcher[] matchers;
        private readonly HashSet<char> startDelimiters;
        private readonly HashSet<char> endDelimiters;
        private int currentIndex;
        private int currentLookaheadIndex;

        public char Current { get; private set; }
        public char Previous { get; private set; }

        public bool EndOfStream => currentLookaheadIndex >= inputString.Length;

        public static char indentOpen = '{';
        public static char indentClose = '}';
        private static StringBuilder indentBuilder = new StringBuilder();

        public static char[] delimiters = new[]
        {
            '[', ']', '(', ')', '{', '}', ';', ':', ',', '.'
        };
        public static CommentMatch commentMatcher = new CommentMatch();
        public static SymbolMatch symbolMatcher = new SymbolMatch();
        public static NumberMatch numberMatcher = new NumberMatch();
        public static StringMatch stringMatcher = new StringMatch();

        public static KeywordMatch validKeywordMatcher = new KeywordMatch
        {
            highlightColor = new Color(0.33f, 0.61f, 0.83f, 1.0f),
            Keywords = new[] { "add", "as", "ascending", "await", "bool", "break", "by", "byte", 
"case", "catch", "char", "checked", "const", "continue", "decimal", "default", "descending", "do", "dynamic", 
"else", "equals", "false", "finally", "float", "for", "foreach", "from", "global", "goto", "group", 
"if", "in", "int", "into", "is", "join", "let", "lock", "long", "new", "null", "object", "on", "orderby", "out", 
"ref", "remove", "return", "sbyte", "select", "short", "sizeof", "stackalloc", "string", 
"switch", "throw", "true", "try", "typeof", "uint", "ulong", "ushort", "var", "where", "while", "yield" }
        };

        public static KeywordMatch invalidKeywordMatcher = new KeywordMatch()
        {
            highlightColor = new Color(0.95f, 0.10f, 0.10f, 1.0f),
            Keywords = new[] { "abstract", "async", "base", "class", "delegate", "enum", "explicit", "extern", "fixed", "get", 
"implicit", "interface", "internal", "namespace", "operator", "override", "params", "private", "protected", "public", 
"using", "partial", "readonly", "sealed", "set", "static", "struct", "this", "unchecked", "unsafe", "value", "virtual", "volatile", "void" }
        };

        // ~~~~~~~ ctor ~~~~~~~

        public CSharpLexer()
        {
            startDelimiters = new HashSet<char>(delimiters);
            endDelimiters = new HashSet<char>(delimiters);

            this.matchers = new Matcher[]
            {
                commentMatcher,
                symbolMatcher,
                numberMatcher,
                stringMatcher,
                validKeywordMatcher,
                invalidKeywordMatcher,
            };

            foreach (Matcher lexer in matchers)
            {
                foreach (char c in lexer.StartChars)
                {
                    if (!startDelimiters.Contains(c))
                        startDelimiters.Add(c);
                }

                foreach (char c in lexer.EndChars)
                {
                    if (!endDelimiters.Contains(c))
                        endDelimiters.Add(c);
                }
            }
        }

        // ~~~~~~~ Lex Matching ~~~~~~~

        public IEnumerable<LexerMatchInfo> GetMatches(string input)
        {
            if (input == null || matchers == null || matchers.Length == 0)
            {
                yield break;
            }

            inputString = input;
            Current = ' ';
            Previous = ' ';
            currentIndex = 0;
            currentLookaheadIndex = 0;

            while (!EndOfStream)
            {
                bool didMatchLexer = false;

                ReadWhiteSpace();

                foreach (Matcher matcher in matchers)
                {
                    int startIndex = currentIndex;

                    bool isMatched = matcher.IsMatch(this);

                    if (isMatched)
                    {
                        int endIndex = currentIndex;

                        didMatchLexer = true;

                        yield return new LexerMatchInfo
                        {
                            startIndex = startIndex,
                            endIndex = endIndex,
                            htmlColor = matcher.HexColor,
                        };

                        break;
                    }
                }

                if (!didMatchLexer)
                {
                    ReadNext();
                    Commit();
                }
            }
        }

        // ~~~~~~~ Indent ~~~~~~~

        public static string GetIndentForInput(string input, int indent, out int caretPosition)
        {
            indentBuilder = new StringBuilder();

            indent += 1;

            bool stringState = false;

            for (int i = 0; i < input.Length; i++)
            {
                if (input[i] == '"')
                {
                    stringState = !stringState;
                }

                if (input[i] == '\n')
                {
                    indentBuilder.Append('\n');
                    for (int j = 0; j < indent; j++)
                    {
                        indentBuilder.Append("\t");
                    }
                }
                else if (input[i] == '\t')
                {
                    continue;
                }
                else if (!stringState && input[i] == indentOpen)
                {
                    indentBuilder.Append(indentOpen);
                    indent++;
                }
                else if (!stringState && input[i] == indentClose)
                {
                    indentBuilder.Append(indentClose);
                    indent--;
                }
                else
                {
                    indentBuilder.Append(input[i]);
                }
            }

            string formattedSection = indentBuilder.ToString();

            caretPosition = formattedSection.Length - 1;

            for (int i = formattedSection.Length - 1; i >= 0; i--)
            {
                if (formattedSection[i] == '\n')
                {
                    continue;
                }

                caretPosition = i;
                break;
            }

            return formattedSection;
        }

        public static int GetIndentLevel(string inputString, int startIndex, int endIndex)
        {
            int indent = 0;

            for (int i = startIndex; i < endIndex; i++)
            {
                if (inputString[i] == '\t')
                {
                    indent++;
                }

                // Check for end line or other characters
                if (inputString[i] == '\n' || inputString[i] != ' ')
                {
                    break;
                }
            }

            return indent;
        }

        // Lexer reading

        public char ReadNext()
        {
            if (EndOfStream)
            {
                return '\0';
            }

            Previous = Current;

            Current = inputString[currentLookaheadIndex];
            currentLookaheadIndex++;

            return Current;
        }

        public void Rollback(int amount = -1)
        {
            if (amount == -1)
            {
                currentLookaheadIndex = currentIndex;
            }
            else
            {
                if (currentLookaheadIndex > currentIndex)
                {
                    currentLookaheadIndex -= amount;
                }
            }

            int previousIndex = currentLookaheadIndex - 1;

            if (previousIndex >= inputString.Length)
            {
                Previous = inputString[inputString.Length - 1];
            }
            else if (previousIndex >= 0)
            {
                Previous = inputString[previousIndex];
            }
            else
            {
                Previous = ' ';
            }
        }

        public void Commit()
        {
            currentIndex = currentLookaheadIndex;
        }

        public bool IsSpecialSymbol(char character, DelimiterType position = DelimiterType.Start)
        {
            if (position == DelimiterType.Start)
            {
                return startDelimiters.Contains(character);
            }

            return endDelimiters.Contains(character);
        }

        private void ReadWhiteSpace()
        {
            while (char.IsWhiteSpace(ReadNext()) == true)
            {
                Commit();
            }

            Rollback();
        }
    }
}