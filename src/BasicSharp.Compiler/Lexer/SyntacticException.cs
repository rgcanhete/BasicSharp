﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BasicSharp.Compiler.Lexer
{
    public class SyntacticException : Exception
    {
        public SlidingText TextSource { get; set; }
        public string Symbol { get; set; }
        public TokenInfo MalformedToken { get; set; }
        public SyntaxKind[] ExpectedSyntaxes { get; set; }
        public string[] ExpectedStrings { get; set; }

        public SyntacticException(string message) : base(message) { }
    }
}
