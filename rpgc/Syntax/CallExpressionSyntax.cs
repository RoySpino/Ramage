﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace rpgc.Syntax
{
    public class CallExpressionSyntax : ExpresionSyntax
    {
        public override TokenKind kind => TokenKind.TK_CALL;
        public SyntaxToken FunctionName { get; }
        public SyntaxToken OpenParen { get; }
        public SeperatedSyntaxList<ExpresionSyntax> Arguments{get; }
        public SyntaxToken CloseParen { get; }

        public CallExpressionSyntax(SyntaxToken functionName, SyntaxToken openParen, SeperatedSyntaxList<ExpresionSyntax> args, SyntaxToken closeParen)
        {
            FunctionName = functionName;
            Arguments = args;
            OpenParen = openParen;
            CloseParen = closeParen;
        }

    }






}
