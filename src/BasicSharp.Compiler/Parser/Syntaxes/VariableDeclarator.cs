﻿using BasicSharp.Compiler.Lexer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BasicSharp.Compiler.Parser.Extensions;

namespace BasicSharp.Compiler.Parser.Syntaxes
{
    public class VariableDeclarator<T> : SyntaxNode
        where T : AssignmentExpression
    {
        public TokenInfo Identifier { get; set; }
        public T Assignment { get; set; }

        public override IEnumerable<TokenInfo> GetInternalTokens()
        {
            yield return Identifier;
            foreach (var item in Assignment.GetTokenEnumerable())
                yield return item;
        }
    }
}
