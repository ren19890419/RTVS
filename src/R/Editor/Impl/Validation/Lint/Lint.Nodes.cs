﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Common.Core;
using Microsoft.Languages.Core.Text;
using Microsoft.R.Core.AST;
using Microsoft.R.Core.AST.Arguments;
using Microsoft.R.Core.AST.Operators;
using Microsoft.R.Core.AST.Statements;
using Microsoft.R.Core.AST.Variables;
using Microsoft.R.Core.Parser;
using Microsoft.R.Core.Tokens;
using Microsoft.R.Editor.Validation.Errors;

namespace Microsoft.R.Editor.Validation.Lint {
    internal partial class LintValidator {
        private static IValidationError AssignmentCheck(IAstNode node, LintOptions options) {
            if (options.AssignmentType) {
                // assignment_linter: checks that ’<-’ is always used for assignment
                if (node is IOperator op && op.OperatorType == OperatorType.Equals) {
                    if (!(op.LeftOperand is NamedArgument)) {
                        return new ValidationWarning(((TokenOperator)op).OperatorToken, Resources.Lint_Assignment, ErrorLocation.Token);
                    }
                }
            }
            return null;
        }

        private static IValidationError CloseCurlySeparateLineCheck(IAstNode node, LintOptions options) {
            // closed_curly_linter: check that closed curly braces should always be 
            // on their own line unless they follow an else
            if (options.CloseCurlySeparateLine) {
                if (node is TokenNode t && t.Token.TokenType == RTokenType.CloseCurlyBrace) {
                    var tp = node.Root.TextProvider;
                    var result = HasLineTextBeforePosition(tp, node.Start);
                    if (!result) {
                        var text = GetLineTextAfterPosition(tp, node.Start);
                        result = text.Length > 0 && !text.Trim().StartsWithOrdinal("else");
                    }
                    if (result) {
                        return new ValidationWarning(node, Resources.Lint_CloseCurlySeparateLine, ErrorLocation.Token);
                    }
                }
            }
            return null;
        }

        private static IValidationError CommaSpacesCheck(IAstNode node, LintOptions options) {
            // commas_linter: check that all commas are followed by spaces, 
            // but do not have spaces before them unless followed by a closing brace
            var warning = false;
            if (options.SpacesAroundComma) {
                if (node is TokenNode t && t.Token.TokenType == RTokenType.Comma) {
                    var tp = node.Root.TextProvider;
                    warning = tp.IsWhitespaceBeforePosition(t.Start);
                    if (!warning && t.End < tp.Length) {
                        var nextChar = tp[t.End];
                        warning = !char.IsWhiteSpace(nextChar) && nextChar != ')' && nextChar != ',';
                    }
                }
            }
            return warning ? new ValidationWarning(node, Resources.Lint_CommaSpaces, ErrorLocation.Token) : null;
        }

        private static IValidationError InfixOperatorsSpacesCheck(IAstNode node, LintOptions options) {
            // infix_spaces_linter: check that all infix operators have spaces around them.
            if (options.SpacesAroundOperators) {
                if (node is IOperator op && !op.IsUnary && op is TokenOperator to) {
                    var tp = node.Root.TextProvider;
                    var t = to.OperatorToken;
                    if (!tp.IsWhitespaceBeforePosition(t.Start) || !tp.IsWhitespaceAfterPosition(t.End - 1)) {
                        return new ValidationWarning(t, Resources.Lint_OperatorSpaces, ErrorLocation.Token);
                    }
                }
            }
            return null;
        }

        private static IValidationError OpenCurlyPositionCheck(IAstNode node, LintOptions options) {
            // open_curly_linter: check that opening curly braces are never on their own line 
            // and are always followed by a newline
            if (options.OpenCurlyPosition) {
                if (node is TokenNode t && t.Token.TokenType == RTokenType.OpenCurlyBrace) {
                    var tp = node.Root.TextProvider;
                    if (!HasLineTextBeforePosition(tp, node.Start) || !tp.IsNewLineAfterPosition(node.End)) {
                        return new ValidationWarning(node, Resources.Lint_OpenCurlyPosition, ErrorLocation.Token);
                    }
                }
            }
            return null;
        }

        private static IValidationError DoubleQuotesCheck(IAstNode node, LintOptions options) {
            // open_curly_linter: check that opening curly braces are never on their own line and are
            // always followed by a newline
            if (options.DoubleQuotes) {
                if (node is TokenNode t && t.Token.TokenType == RTokenType.String) {
                    if (node.Root.TextProvider[node.Start] != '\"') {
                        return new ValidationWarning(node, Resources.Lint_DoubleQuotes, ErrorLocation.Token);
                    }
                }
            }
            return null;
        }

        private static IValidationError SpaceBeforeOpenBraceCheck(IAstNode node, LintOptions options) {
            // spaces_left_parentheses_linter: check that all left parentheses have a space 
            // before them unless they are in a function call.
            if (options.SpaceBeforeOpenBrace && node is TokenNode t) {
                if (t.Token.TokenType == RTokenType.OpenBrace && node.Parent is IKeywordExpression) {
                    var tp = node.Root.TextProvider;
                    if (!tp.IsWhitespaceBeforePosition(node.Start)) {
                        return new ValidationWarning(t.Token, Resources.Lint_SpaceBeforeOpenBrace, ErrorLocation.Token);
                    }
                }
            }
            return null;
        }

        private static IValidationError SpacesInsideParenthesisCheck(IAstNode node, LintOptions options) {
            // There should be no space after (, [ or [[ and no space before ), ] or ]]
            // unless ] or ]] is preceded by a comma as in x[1, ]
            if (options.SpacesInsideParenthesis && node is TokenNode t) {
                var tp = node.Root.TextProvider;
                switch (t.Token.TokenType) {
                    case RTokenType.OpenBrace:
                    case RTokenType.OpenSquareBracket:
                    case RTokenType.OpenDoubleSquareBracket:
                        // x[1, OK x( 2) is not
                        if (!tp.IsNewLineAfterPosition(node.End) && tp.IsWhitespaceAfterPosition(node.End - 1)) {
                            var lineEnd = tp.IndexOf('\n', node.End);
                            lineEnd = lineEnd >= 0 ? lineEnd : tp.Length;
                            var text = tp.GetText(TextRange.FromBounds(node.End, lineEnd));
                            var wsEnd = text.IndexWhere(ch => !char.IsWhiteSpace(ch)).FirstOrDefault();
                            wsEnd = wsEnd > 0 ? wsEnd + node.End : tp.Length;
                            return new ValidationWarning(TextRange.FromBounds(node.End, wsEnd), Resources.Lint_SpaceAfterLeftParenthesis, ErrorLocation.Token);
                        }
                        break;
                    case RTokenType.CloseBrace:
                        // () is OK, (,,,) is OK, x( )  is not OK. But we do allow line break before )
                        if (tp.IsWhitespaceBeforePosition(node.Start) && !tp.IsNewLineBeforePosition(node.Start)) {
                            var i = node.Start - 1;
                            for (; i >= 0; i--) {
                                if (!char.IsWhiteSpace(tp[i]) || tp[i] == '\r' || tp[i] == '\n') {
                                    i++;
                                    break;
                                }
                            }
                            i = Math.Max(i, 0);
                            return new ValidationWarning(TextRange.FromBounds(i, node.Start), Resources.Lint_SpaceBeforeClosingBrace, ErrorLocation.Token);
                        }
                        break;
                    case RTokenType.CloseSquareBracket:
                    case RTokenType.CloseDoubleSquareBracket:
                        // x[1] is OK, x[1,] is not OK, should be x[1, ]
                        var prevChar = node.Start > 0 ? tp[node.Start - 1] : '\0';
                        if (!tp.IsWhitespaceAfterPosition(node.End - 1) && prevChar == ',') {
                            return new ValidationWarning(node, Resources.Lint_NoSpaceBetweenCommaAndClosingBracket, ErrorLocation.Token);
                        }
                        break;
                }
            }
            return null;
        }

        private static IValidationError SpaceAfterFunctionNameCheck(IAstNode node, LintOptions options) {
            if (options.NoSpaceAfterFunctionName && node is FunctionCall fc) {
                var tp = node.Root.TextProvider;
                if (fc.RightOperand is Variable v && tp.IsWhitespaceAfterPosition(v.End - 1)) {
                    return new ValidationWarning(TextRange.FromBounds(v.End, fc.OpenBrace.Start), Resources.Lint_SpaceAfterFunctionName, ErrorLocation.Token);
                }
            }
            return null;
        }

        private static IValidationError SemicolonCheck(IAstNode node, LintOptions options) {
            if (options.Semicolons && node is TokenNode t && t.Token.TokenType == RTokenType.Semicolon) {
                return new ValidationWarning(node, Resources.Lint_Semicolons, ErrorLocation.Token);
            }
            return null;
        }

        private static IValidationError MultipleStatementsCheck(IAstNode node, LintOptions options) {
            if (options.MultipleStatements && node is TokenNode t && t.Token.TokenType == RTokenType.Semicolon) {
                var tp = node.Root.TextProvider;
                if (!tp.IsNewLineAfterPosition(node.End)) {
                    // # comment is OK but comments are not part of the AST.
                    var lineBreakIndex = tp.IndexOf('\n', node.End);
                    var trailingTextEnd = lineBreakIndex >= 0 ? lineBreakIndex : tp.Length;
                    var trailingText = tp.GetText(TextRange.FromBounds(node.End, trailingTextEnd));
                    var tokens = new RTokenizer().Tokenize(trailingText);
                    var offendingTokens = tokens.Where(x => x.TokenType != RTokenType.Comment);
                    if (offendingTokens.Any()) {
                        var squiggle = TextRange.FromBounds(node.End + offendingTokens.First().Start, node.End + offendingTokens.Last().End);
                        return new ValidationWarning(squiggle, Resources.Lint_MultipleStatementsInLine, ErrorLocation.Token);
                    }
                }
            }
            return null;
        }

        private static IValidationError TrueFalseNamesCheck(IAstNode node, LintOptions options) {
            // Use TRUE and FALSE instead of T and F
            if (options.TrueFalseNames) {
                if (node is TokenNode t && t.Token.TokenType == RTokenType.Logical) {
                    if (t.Token.Length == 1) {
                        return new ValidationWarning(t.Token, Resources.Lint_TrueFalseNames, ErrorLocation.Token);
                    }
                }
            }
            return null;
        }


        private static IEnumerable<IValidationError> NameCheck(IAstNode node, LintOptions options) {
            // camel_case_linter: check that objects are not in camelCase.
            // snake_case_linter: check that objects are not in snake_case.
            // multiple_dots_linter: check that objects do not have.multiple.dots.
            // object_length_linter: check that objects do are not very long.not have.multiple.dots.

            if (node is TokenNode t && t.Token.TokenType == RTokenType.Identifier) {
                var list = new List<IValidationError>();
                var text = node.Root.TextProvider.GetText(node);
                if (options.UpperCase && IsUpperCase(text)) {
                    list.Add(new ValidationWarning(node, Resources.Lint_Uppercase, ErrorLocation.Token));
                }
                if (options.CamelCase && IsCamelCase(text)) {
                    list.Add(new ValidationWarning(node, Resources.Lint_CamelCase, ErrorLocation.Token));
                }
                if (options.SnakeCase && IsSnakeCase(text)) {
                    list.Add(new ValidationWarning(node, Resources.Lint_SnakeCase, ErrorLocation.Token));
                }
                if (options.PascalCase && IsPascalCase(text)) {
                    list.Add(new ValidationWarning(node, Resources.Lint_PascalCase, ErrorLocation.Token));
                }
                if (options.MultipleDots && HasMultipleDots(text)) {
                    list.Add(new ValidationWarning(node, Resources.Lint_MultileDots, ErrorLocation.Token));
                }
                if (options.NameLength && text.Length > options.MaxNameLength) {
                    list.Add(new ValidationWarning(node, Resources.Lint_NameTooLong.FormatInvariant(text.Length, options.MaxNameLength), ErrorLocation.Token));
                }
                return list;
            }
            return Enumerable.Empty<IValidationError>();
        }

        private static bool IsCamelCase(string text)
            => text.Length > 0 && char.IsLower(text[0]) && text.Any(x => char.IsUpper(x));

        private static bool IsPascalCase(string text)
            => text.Length > 0 && char.IsUpper(text[0]) && text.Any(x => char.IsLower(x));

        private static bool IsSnakeCase(string text)
            => text.Length > 0 && text.Any(x => x == '_');

        private static bool IsUpperCase(string text)
            => text.Length > 0 && !text.Any(x => char.IsLower(x));

        private static bool HasMultipleDots(string text)
            => text.Length > 0 && text.Count(x => x == '.') > 1;

        private static bool HasLineTextBeforePosition(ITextProvider tp, int position) {
            for (var i = position - 1; i >= 0; i--) {
                if (tp[i] == '\n' || tp[i] == '\r') {
                    break;
                }
                if (!char.IsWhiteSpace(tp[i])) {
                    return true;
                }
            }
            return false;
        }
        private static bool HasLineTextAfterPosition(ITextProvider tp, int position) {
            for (var i = position + 1; i < tp.Length; i++) {
                if (tp[i] == '\n' || tp[i] == '\r') {
                    break;
                }
                if (!char.IsWhiteSpace(tp[i])) {
                    return true;
                }
            }
            return false;
        }

        private static string GetLineTextAfterPosition(ITextProvider tp, int position) {
            var i = position + 1;
            for (; i < tp.Length; i++) {
                if (tp[i] == '\n' || tp[i] == '\r') {
                    break;
                }
            }
            return tp.GetText(TextRange.FromBounds(i + 1, i));
        }
    }
}
