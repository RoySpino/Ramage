﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using rpgc.Symbols;
using System.Threading.Tasks;

namespace rpgc.Syntax
{
    public class StructNode
    {
        public int linePos;
        public int chrPos;
        public string symbol;
        public int factor;

        public StructNode(int l, int ch, string sym)
        {
            linePos = l;
            chrPos = (l + ch);
            symbol = sym;
            factor = 0;
        }

        public bool isLeftJustified()
        {
            return (symbol[0] == 32);
        }

        public bool isRightJustified()
        {
            int endIdx;

            endIdx = symbol.Length - 1;

            return (symbol[endIdx] == 32);
        }
    }

    public class StructCard
    {
        public int LinePos;
        public int LineSiz;
        public string Line;

        public StructCard(string l, int lPos)
        {
            Line = l;
            LinePos = lPos;
            LineSiz = l.Length;
        }
    }

    // ////////////////////////////////////////////////////////////////////////////
    // /////     /////     /////     /////     /////     /////     /////     /////
    // //////////////////////////////////////////////////////////////////////////
    public static class Decimator
    {
        static char specChkStr = 'H';
        static bool isProcSection = false;
        static TokenKind kind;
        static char curChar;
        static object Value;
        static int start, lineStart;
        static string tmpVal;
        static DiagnosticBag diagnostics = null;
        static int assignmentCnt;
        static bool onEvalLine, onBooleanLine, isOpenProc = false;
        static int parenCnt = 0;
        static string lineType = "", curOp="", prevOp="";
        static string factor;
        static int pos, sSize, linePos;
        private static List<SyntaxToken> localTokenLst = new List<SyntaxToken>();
        private static List<SyntaxToken> localTokenLst2 = new List<SyntaxToken>();
        private static List<SyntaxToken> localCSpecDclr = new List<SyntaxToken>();
        private static int ITagCnt = 0;

        private static bool isGoodSpec(char spec, int line)
        {
            char prevSpec;
            Dictionary<char, int> specVal2 = new Dictionary<char, int>() { { 'H', 1 }, { 'F', 2 }, { 'D', 3 }, { 'I', 4 }, { 'C', 5 }, { 'O', 6 }, { 'P', 7 } };
            Dictionary<char, int> procSpec = new Dictionary<char, int>() { { 'D', 3 }, { 'C', 5 }, { 'P', 7 } };
            Dictionary<char, int> mainDic = null;

            // standardize dictionary
            if (isProcSection == false)
                mainDic = specVal2;
            else
                mainDic = procSpec;

            // invalid specification
            if (mainDic.ContainsKey(spec) == false)
            {
                diagnostics.reportBadSpec(spec, line);
                return false;
            }

            // spec is the same 
            if (spec == specChkStr)
                return true;

            // within the main procedure AND spec are not the same 
            if (isProcSection == false)
            {
                if (mainDic[spec] >= mainDic[specChkStr])
                {
                    // start of procedure section reset to D and return true
                    if (spec == 'P')
                    {
                        isProcSection = true;
                        isOpenProc = true;
                        specChkStr = 'D';
                    }
                    else
                        specChkStr = spec;

                    return true;
                }
                return false;
            }
            else
            {
                // in procedure section AND spec are not the same
                if (mainDic[spec] >= mainDic[specChkStr])
                {
                    // start of a procedure return true and
                    // reset spec to accept D or C specs
                    if (spec == 'P' && isOpenProc == true)
                        specChkStr = 'D';
                    else
                    {
                        // marking an end to a procedure 
                        // do not reset spec but set flag to accept only another P spec
                        if (spec == 'P' && isOpenProc == true)
                            isOpenProc = true;
                        else
                            specChkStr = spec;
                    }

                    return true;
                }
                return false;
            }
        }

        // //////////////////////////////////////////////////////////////////////////
        private static int computeCharPos(int pos)
        {
            return lineStart + pos;
        }

        // //////////////////////////////////////////////////////////////////////////
        private static List<StructNode> decimateCSpec(int lineNo, string line)
        {
            List<StructNode> ret = new List<StructNode>();
            StructNode tnode;
            int strLen;
            string sym;
            bool isOnEvalLine = false, doEvalSlice = false;

            int[,] slicer = {
                {1, 2},
                {3, 1},
                {4, 2},
                {6, 14},
                {20, 10},
                {30, 14},
                {44, 14},
                {58, 5},
                {63, 2},
                {65, 2},
                {67, 2},
                {69, 2}
            };

            // check spec position
            if (isGoodSpec(line[0], lineNo) == false)
                diagnostics.reportWrongSpecLoc('C', specChkStr, lineNo);

            strLen = line.Length - 1;

            // setup for extended factor 2
            // otherwise use traditinal rpg 14 character long factors
            sym = line.Substring(20, 10).Trim();
            isOnEvalLine = SyntaxFacts.isExtededFector2Keyword(sym);

            for (int i = 0; i < 12; i++)
            {
                if (strLen <= 0)
                    break;

                if (doEvalSlice == false)
                {
                    // slice standard RPG C spec
                    if (strLen >= slicer[i, 1])
                        sym = line.Substring(slicer[i, 0], slicer[i, 1]);
                    else
                        sym = line.Substring(slicer[i, 0]);
                    strLen -= slicer[i, 1];
                }
                else
                {
                    // slice evaluation
                    sym = line.Substring(slicer[i, 0]);
                    strLen = 0;
                }

                // trim string
                if (i == 7 || i == 8)
                    sym = sym.TrimStart();
                else
                    sym = sym.TrimEnd();

                // check if 
                doEvalSlice = (i == 4 && isOnEvalLine == true);

                tnode = new StructNode(lineNo, computeCharPos(slicer[i, 0]), sym.TrimEnd());
                switch(i)
                {
                    case 3:
                        tnode.factor = 1;
                        break;
                    case 4:
                        tnode.factor = 4;
                        break;
                    case 5:
                        tnode.factor = 2;
                        break;
                    case 6:
                        tnode.factor = 3;
                        break;
                    default:
                        tnode.factor = 0;
                        break;
                }

                ret.Add(tnode);
            }

            return ret;
        }

        // //////////////////////////////////////////////////////////////////////////
        private static List<StructNode> decimateDSpec(int lineNo, string line)
        {
            List<StructNode> ret = new List<StructNode>();
            int strLen;
            string sym;

            int[,] slicer = {
                {1, 15},
                {16, 1},
                {17, 1},
                {18, 2},
                {20, 7},
                {27, 7},
                {34, 1},
                {35, 2},
                {38, 33}
            };

            // check spec position
            if (isGoodSpec(line[0], lineNo) == false)
                diagnostics.reportWrongSpecLoc('D', specChkStr, lineNo);

            strLen = line.Length - 1;

            for (int i = 0; i < 9; i++)
            {
                if (strLen <= 0)
                    break;

                // slice string according to column position
                if (strLen >= slicer[i, 1])
                    sym = line.Substring(slicer[i, 0], slicer[i, 1]);
                else
                    sym = line.Substring(slicer[i, 0]);
                strLen -= slicer[i, 1];

                // trim string
                sym = sym.Trim();
                /*
                if (i == 4 || i == 5 || i == 7)
                    sym = sym.TrimStart();
                else
                    sym = sym.TrimEnd();
                */

                ret.Add(new StructNode(lineNo, computeCharPos(slicer[i, 0]), sym));
            }

            return ret;
        }

        // //////////////////////////////////////////////////////////////////////////
        private static List<StructNode> decimatePSpec(int lineNo, string line)
        {
            List<StructNode> ret = new List<StructNode>();
            int strLen;
            string sym;

            int[,] slicer = {
                {1, 15},
                {18, 1},
                {30, 4},
                {34, 1},
                {35, 2},
                {38, 33}
            };

            // check spec position
            if (isGoodSpec(line[0], lineNo) == false)
                diagnostics.reportWrongSpecLoc('P', specChkStr, lineNo);

            strLen = line.Length - 1;

            for (int i = 0; i < 6; i++)
            {
                if (strLen <= 0)
                    break;

                // slice string according to column position
                if (strLen >= slicer[i, 1])
                    sym = line.Substring(slicer[i, 0], slicer[i, 1]);
                else
                    sym = line.Substring(slicer[i, 0]);
                strLen -= slicer[i, 1];

                ret.Add(new StructNode(lineNo, computeCharPos(slicer[i, 0]), sym));
            }

            return ret;
        }

        // //////////////////////////////////////////////////////////////////////////
        private static List<StructNode> decimateFSpec(int lineNo, string line)
        {
            List<StructNode> ret = new List<StructNode>();
            int strLen;
            string sym;

            int[,] slicer = {
                {1, 10},
                {11,1},
                {12,1},
                {13,1},
                {14,1},
                {15,1},
                {16,1},
                {22,1},
                {28,1},
                {30,7},
                {38, 33}
            };

            // check spec position
            if (isGoodSpec(line[0], lineNo) == false)
                diagnostics.reportWrongSpecLoc('F', specChkStr, lineNo);

            strLen = line.Length - 1;

            for (int i = 0; i < 11; i++)
            {
                if (strLen <= 0)
                    break;

                if (strLen >= slicer[i, 1])
                    sym = line.Substring(slicer[i, 0], slicer[i, 1]);
                else
                    sym = line.Substring(slicer[i, 0]);
                strLen -= slicer[i, 1];

                ret.Add(new StructNode(lineNo, computeCharPos(slicer[i, 0]), sym));
            }

            return ret;
        }

        // //////////////////////////////////////////////////////////////////////////
        private static List<StructNode> decimateHSpec(int lineNo, string line)
        {
            List<StructNode> ret = new List<StructNode>();
            int strLen;
            string sym;

            int[,] slicer = {
                {1, 79}
            };

            // check spec position
            if (isGoodSpec(line[0], lineNo) == false)
                diagnostics.reportWrongSpecLoc('H', specChkStr, lineNo);

            strLen = line.Length - 1;

            for (int i = 0; i < slicer.Length; i++)
            {
                if (strLen <= 0)
                    break;

                if (strLen >= slicer[i, 1])
                    sym = line.Substring(slicer[i, 0], slicer[i, 1]);
                else
                    sym = line.Substring(slicer[i, 0]);
                strLen -= slicer[i, 1];

                ret.Add(new StructNode(lineNo, computeCharPos(slicer[i, 0]), sym));
            }

            return ret;
        }

        // //////////////////////////////////////////////////////////////////////////
        internal static List<SyntaxToken> performCSpecVarDeclar(string[] arr)
        {
            List<StructNode> nlist = new List<StructNode>();
            List<string> lstVars = new List<string>();
            SyntaxToken[] tNodeArr;
            string decSize, intSize, varName, chkr;
            string[] declarLines;


            // find all C Spec declaration lines
            //declarLines = arr.Where(csl => csl.StartsWith("C") == true && csl.Length >= 64).ToArray();
            declarLines = arr.Where(csl => csl.StartsWith("C") == true).ToArray()
                             .Where(gln => gln.Length >= 64).ToArray()
                             .Where(dln => dln[58] != ' ').ToArray();

            // no declared lines found do nothing
            if (declarLines == null)
                return new List<SyntaxToken>(new SyntaxToken[] { new SyntaxToken(TokenKind.TK_SPACE, 0, 0, "") });


            // capatalize each line
            for (int i = 0; i < declarLines.Length; i++)
                declarLines[i] = declarLines[i].ToUpper();

            // normalize l
            foreach (string ln in declarLines)
            {
                varName = ln.Substring(44, 14).Trim();

                // chekc if variable is already declared
                chkr = lstVars.Where(vn => vn == varName).FirstOrDefault();
                if (chkr != null)
                    continue;

                // add varialbe to declared list
                lstVars.Add(varName);

                if (ln.Length == 64)
                {
                    // create string declarations ONLY
                    intSize = ln.Substring(55, 5);

                    tNodeArr = new SyntaxToken[] {
                    new SyntaxToken(TokenKind.TK_VARDECLR, 0, 0, ""),
                    new SyntaxToken(TokenKind.TK_IDENTIFIER,  0, 0, varName),
                    new SyntaxToken(TokenKind.TK_STRING,  0, 0, "") };
                }
                else
                {
                    // create declarations for Ints and floats
                    intSize = ln.Substring(55, 5);
                    decSize = ln.Substring(63, 2);

                    tNodeArr = new SyntaxToken[] {
                    new SyntaxToken(TokenKind.TK_VARDECLR, 0, 0, ""),
                    new SyntaxToken(TokenKind.TK_IDENTIFIER,  0, 0, varName),
                    new SyntaxToken(TokenKind.TK_ZONED,  0, 0, "") };
                }

                localCSpecDclr.AddRange(tNodeArr);
            }

            //return localCSpecDclr;
            return new List<SyntaxToken>(new SyntaxToken[] { new SyntaxToken(TokenKind.TK_SPACE, 0, 0, "") });
        }

        // ////////////////////////////////////////////////////////////////////////////////////
        private static void nextChar()
        {
            pos += 1;

            if (pos >= sSize)
                curChar = '\0';
            else
            {
                curChar = factor[pos];

                if (curChar == '\n')
                {
                    pos += 1;
                    start = 0;
                    curChar = factor[pos];
                }
            }
        }

        // ////////////////////////////////////////////////////////////////////////////////////
        private static char peek(int offset)
        {
            int index;

            index = pos + offset;

            if (index >= sSize)
                return '\0';
            else
                return factor[index];
        }

        // ////////////////////////////////////////////////////////////////////////////////////
        public static List<SyntaxToken> doLex(StructNode factor_, string KeyWord = "")
        {
            List<SyntaxToken> ret = new List<SyntaxToken>();
            string symbol = "", line;
            int sz;

            pos = -1;
            line = factor_.symbol;
            start = factor_.chrPos;
            linePos = factor_.linePos;
            factor = line;
            sz = line.Length;
            sSize = sz;
            assignmentCnt = 0;

            nextChar();

            while (pos != sz)
            {
                // exit decimator lexer
                if (curChar < 32)
                    break;

                switch (curChar)
                {
                    case '+':
                        start = pos;
                        kind = TokenKind.TK_ADD;
                        Value = "+";
                        nextChar();
                        break;
                    case '-':
                        start = pos;
                        kind = TokenKind.TK_SUB;
                        Value = "-";
                        nextChar();
                        break;
                    case '*':
                        symbol = readCompilerConstantsOrMult();// (line, ref i);
                        Value = symbol;
                        break;
                    case '/':
                        start = pos;
                        kind = TokenKind.TK_DIV;
                        Value = "/";
                        nextChar();
                        break;
                    case '(':
                        start = pos;
                        kind = TokenKind.TK_PARENOPEN;
                        Value = "(";
                        parenCnt += 1;
                        nextChar();
                        break;
                    case ')':
                        start = pos;
                        kind = TokenKind.TK_PARENCLOSE;
                        Value = ")";
                        parenCnt -= 1;
                        nextChar();
                        break;
                    case ':':
                        start = pos;
                        kind = TokenKind.TK_COLON;
                        Value = ":";
                        nextChar();
                        break;
                    case '=':
                        start = pos;
                        kind = getAssignmentOrComparisonToken();
                        Value = "=";
                        nextChar();
                        break;
                    case '<':
                        start = pos;
                        kind = getLessGreaterThanOperator('<', peek(1));
                        Value = tmpVal;
                        nextChar();
                        break;
                    case '>':
                        start = pos;
                        kind = getLessGreaterThanOperator('>', peek(1));
                        Value = tmpVal;
                        nextChar();
                        break;
                    case '%':
                        symbol = readBuiltInFunctions();//readBuiltInFunctions(line, ref i);
                        break;
                    case '\'':
                        readString();// (line, ref i);
                        break;
                    case '@':
                    case '#':
                    case '$':
                        symbol = readIdentifierOrKeyword();//(line, ref i);
                        Value = symbol;
                        break;
                    case '0':
                    case '1':
                    case '2':
                    case '3':
                    case '4':
                    case '5':
                    case '6':
                    case '7':
                    case '8':
                    case '9':
                        symbol = readNumberToken();// (line, ref i);
                        Value = symbol;
                        break;
                    case ' ':
                    case '\0':
                    case '\n':
                    case '\t':
                    case '\r':
                        readWiteSpace();//(line, ref i);
                        symbol = " ";
                        break;
                    default:
                        if (char.IsLetter(curChar) == true)
                        {
                            symbol = readIdentifierOrKeyword();// (line, ref i);
                        }
                        else
                        {
                            if (char.IsWhiteSpace(curChar) == true)
                            {
                                symbol = "";
                                readWiteSpace();//(line, ref i);
                            }
                            else
                            {
                                diagnostics.reportBadCharacter(curChar, 1);
                                symbol = curChar.ToString();
                            }
                        }
                        Value = symbol;
                        break;
                }

                ret.Add(new SyntaxToken(kind, linePos, (start + factor_.chrPos), Value, start));
            }

            return ret;
        }

        // ////////////////////////////////////////////////////////////////////////////////////
        private static void readString()
        {
            bool isInString;
            int charCnt;
            string text;

            // record current position and skip first single quoat
            start += pos;
            nextChar();

            isInString = true;
            charCnt = 0;
            text = "";

            while (isInString)
            {
                charCnt += 1;
                if (curChar == '\'' && peek(1) != '\'')
                    break;

                switch (curChar)
                {
                    case '\0':
                    case '\n':
                    case '\r':
                        diagnostics.reportBadString(new TextSpan(start, charCnt, linePos, pos));
                        isInString = false;
                        break;
                    case '\'':
                        text += curChar;
                        nextChar();
                        break;
                    default:
                        text += curChar;
                        break;
                }

                nextChar();
            }

            nextChar();
            kind = TokenKind.TK_STRING;
            Value = text;
        }

        // ////////////////////////////////////////////////////////////////////////////////////
        private static string readNumberToken()
        {
            string symbol = "";
            int intDummy;

            start += pos;

            while (char.IsDigit(curChar) == true)
            {
                symbol += curChar;
                nextChar();
            }

            if (int.TryParse(symbol, out intDummy) == false)
                diagnostics.reportInvalidNumber(symbol, TypeSymbol.Integer, start, symbol.Length);

            kind = TokenKind.TK_INTEGER;
            Value = intDummy;

            return symbol;
        }

        // ////////////////////////////////////////////////////////////////////////////////////
        private static void readWiteSpace()
        {
            string symbol = "";

            start += pos;

            // skip whitespace
            while (char.IsWhiteSpace(curChar) == true)
            {
                symbol += curChar;
                nextChar();
            }

            Value = "";
            kind = TokenKind.TK_SPACE;
        }

        // ////////////////////////////////////////////////////////////////////////////////////
        private static string readIdentifierOrKeyword()
        {
            string symbol = "";

            start += pos;
            while (char.IsLetterOrDigit(curChar) || curChar == '#' || curChar == '@' || curChar == '$' || curChar == '_')
            {
                symbol += curChar;
                nextChar();
            }

            symbol = symbol.ToUpper();

            // assign keyword token
            kind = SyntaxFacts.getKeywordKind(symbol);

            // chech if symbol is a valid variabel name
            if (kind == TokenKind.TK_IDENTIFIER)
            {
                // chech if symbol is a valid variabel name
                if (SyntaxFacts.isValidVariable(symbol))
                    Value = symbol;
                else
                    kind = TokenKind.TK_BADTOKEN;
            }

            return symbol;
        }

        // ////////////////////////////////////////////////////////////////////////////////////
        // special indicators and constants defalts to mult if nothing is found
        private static string readCompilerConstantsOrMult()
        {
            string symbol, peekStr;
            char peekChar;
            int peekPos;

            start += pos;
            peekPos = 0;
            symbol = "*";
            peekStr = "";

            while (true)
            {
                peekChar = peek(peekPos);
                peekPos += 1;

                if (SyntaxFacts.isCharLiteralOrControl(peekChar) == false && peekChar != '*')
                    break;

                peekStr += peekChar;
            }
            kind = SyntaxFacts.getBuiltInIndicator(peekStr);

            if (kind != TokenKind.TK_BADTOKEN)
            {
                for (int i = 0; i < peekStr.Length; i++)
                {
                    nextChar();
                }

                Value = peekStr;
                return peekStr;
            }
            else
            {
                nextChar();
                kind = TokenKind.TK_MULT;
                Value = "*";
            }

            kind = TokenKind.TK_MULT;
            return peekStr;
        }

        // ////////////////////////////////////////////////////////////////////////////////////
        private static string readBuiltInFunctions()
        {
            string symbol = "";

            start += pos;
            symbol = "%";

            nextChar();
            while (char.IsLetterOrDigit(curChar) == true)
            {
                symbol += curChar;
                nextChar();
            }

            Value = symbol.Trim().ToUpper();
            kind = SyntaxFacts.getBuiltInFunction(Value.ToString());

            return symbol;
        }

        // ////////////////////////////////////////////////////////////////////////////////////
        private static void ignoreCommentLine(string line, ref int pos)
        {
            while (true)
            {
                pos += 1;
                if (line[pos] == '\n' || line[pos] == '\0' || line[pos] == '\r')
                    break;
            }

            kind = TokenKind.TK_SPACE;
        }

        // ////////////////////////////////////////////////////////////////////////////////////
        private static TokenKind getAssignmentOrComparisonToken()
        {
            TokenKind ret;

            // if the [=] is inside a parethisies then its a comparison
            onEvalLine = (parenCnt == 0 && onBooleanLine == false);

            // check if the current line is a comparison or assignment
            if (onEvalLine == true && assignmentCnt < 1)
            {
                // first = is an assignment all others are comparisons
                ret = TokenKind.TK_ASSIGN;
                assignmentCnt += 1;
                onEvalLine = false;
            }
            else
                ret = TokenKind.TK_EQ;

            // reset boolean
            onEvalLine = true;

            return ret;
        }

        // ////////////////////////////////////////////////////////////////////////////////////
        private static TokenKind getLessGreaterThanOperator(char first, char op)
        {
            TokenKind ret;

            // check if the fisrt char is a Less than symbol
            if (first == '<')
                switch (op)
                {
                    case '>':
                        ret = TokenKind.TK_NE;
                        tmpVal = ("" + first + op);
                        pos += 1;
                        break;
                    case '=':
                        ret = TokenKind.TK_LE;
                        tmpVal = ("" + first + op);
                        pos += 1;
                        break;
                    default:
                        ret = TokenKind.TK_LT;
                        tmpVal = ("" + first);
                        break;
                }
            else
                // for greater than symbols
                switch (op)
                {
                    case '=':
                        ret = TokenKind.TK_GE;
                        tmpVal = ("" + first + op);
                        pos += 1;
                        break;
                    default:
                        ret = TokenKind.TK_GT;
                        tmpVal = ("" + first);
                        break;
                }

            return ret;
        }

        // ////////////////////////////////////////////////////////////////////////////////////

        private static StructNode getComparisonInd(StructNode node1, StructNode node2, StructNode node3)
        {
            StructNode tmp;

            // get the first non blank symbol
            if (node1.symbol.Trim().Length > 0)
                tmp = node1;
            else
            {
                if (node2.symbol.Trim().Length > 0)
                    tmp = node2;
                else
                    tmp = node3;
            }

            // convert two digit indicator to standard indicator
            tmp.symbol = $"*IN{tmp.symbol}";

            return tmp;
        }

        // ////////////////////////////////////////////////////////////////////////////////////
        private static List<SyntaxToken> normalizeConditinalIndicators(StructNode Col2, StructNode Col3)
        {
            string N, ind01, condition;
            List<SyntaxToken> ret = new List<SyntaxToken>();

            // rewrite column 2 
            if (Col2.symbol == "")
                N = " = *On";
            else
                N = " = *Off";

            // convert two didget indicator to freeformat indicator
            ind01 = Col3.symbol;
            if (ind01 != "")
                ind01 = $"*in{ind01}";

            // build comparison string
            condition = ind01 + N;

            // lex the condition line
            ret.Add(new SyntaxToken(TokenKind.TK_IF, Col2.linePos, Col2.chrPos, "IF"));
            ret.AddRange(doLex(new StructNode(Col3.linePos, Col3.chrPos, condition)));
            return ret;
        }

        // //////////////////////////////////////////////////////////////////////////////////
        private static SyntaxToken getComparisonOpCode(StructNode node)
        {
            string op;

            op = node.symbol;
            op = op.Substring(op.Length - 2);

            switch (op)
            {
                case "EQ":
                    return new SyntaxToken(TokenKind.TK_EQ, node.linePos, computeCharPos(node.chrPos), node.symbol);
                case "NE":
                    return new SyntaxToken(TokenKind.TK_NE, node.linePos, computeCharPos(node.chrPos), node.symbol);

                case "LT":
                    return new SyntaxToken(TokenKind.TK_LT, node.linePos, computeCharPos(node.chrPos), node.symbol);
                case "GT":
                    return new SyntaxToken(TokenKind.TK_GT, node.linePos, computeCharPos(node.chrPos), node.symbol);

                case "GE":
                    return new SyntaxToken(TokenKind.TK_GE, node.linePos, computeCharPos(node.chrPos), node.symbol);
                case "LE":
                    return new SyntaxToken(TokenKind.TK_LE, node.linePos, computeCharPos(node.chrPos), node.symbol);
                default:
                    return new SyntaxToken(TokenKind.TK_BADTOKEN, node.linePos, computeCharPos(node.chrPos), "");
            }
        }

        // //////////////////////////////////////////////////////////////////////////////////
        private static StructNode leftJustified(List<StructNode> lst)
        {
            for (int i = 3; i < 7; i++)
                if (i < lst.Count)
                    if (string.IsNullOrEmpty(lst[i].symbol) == false)
                        if (lst[i].isLeftJustified() == true)
                            return lst[i];

            return null;
        }

        // //////////////////////////////////////////////////////////////////////////////////
        private static SyntaxToken reportCSpecPositionError(StructNode snode)
        {
            // one of the factors is not left justified
            diagnostics.reportNotLeftJustified(new TextSpan(snode.chrPos, snode.symbol.Length, snode.chrPos, snode.linePos), snode.factor, snode.linePos);

            return new SyntaxToken(TokenKind.TK_BADTOKEN, snode.linePos, computeCharPos(snode.chrPos), snode.symbol);
        }

        // ////////////////////////////////////////////////////////////////////////////
        public static List<StructNode> doDecimation(int lineNo, string line)
        {
            char Specification;
            string tmp;

            tmp = line.ToUpper().PadRight(72);
            Specification = line[0];

            // begin decimation
            switch (Specification)
            {
                case 'H':
                    return decimateHSpec(lineNo, tmp);
                case 'F':
                    return decimateFSpec(lineNo, tmp);
                case 'D':
                    return decimateDSpec(lineNo, tmp);
                case 'I':
                    return null;
                case 'C':
                    return decimateCSpec(lineNo, tmp);
                case 'O':
                    return null;
                case 'P':
                    return decimatePSpec(lineNo, tmp);
                default:
                    return null;
            }
        }

        // ////////////////////////////////////////////////////////////////////////////
        public static List<SyntaxToken> doDecimation2(int lineNo, int charPos, string line, ref DiagnosticBag diag)
        {
            char Specification;
            string tmp;
            List<StructNode> lst;
            List<SyntaxToken> ret;

            // setup global diagnostic bag
            diagnostics = diag;

            // setup line
            tmp = line.PadRight(72);
            Specification = line[0];
            lineStart = charPos;

            // end of file line
            if (line[0] == '\0')
                return new List<SyntaxToken>(new SyntaxToken[] { new SyntaxToken(TokenKind.TK_EOI, lineNo, charPos, "_") });

            // do not try to decimate a blank line
            if (tmp[0] == ' ')
                return new List<SyntaxToken>(new SyntaxToken[] { new SyntaxToken(TokenKind.TK_SPACE, lineNo, 0, "") });

            // handle comments
            if (tmp[1] == '*' || (tmp[1] == '/' && tmp[2] == '/'))
                return new List<SyntaxToken>(new SyntaxToken[] { new SyntaxToken(TokenKind.TK_SPACE, lineNo, 0, "") });



            // begin decimation
            switch (Specification)
            {
                case 'H':
                    lst = decimateHSpec(lineNo, tmp);
                    ret = dSpecRectifier(lst);
                    break;
                case 'F':
                    lst = decimateFSpec(lineNo, tmp);
                    ret = new List<SyntaxToken>(new SyntaxToken[] { new SyntaxToken(TokenKind.TK_BADTOKEN, lst[0].linePos, lst[0].chrPos, "") });
                    break;
                case 'D':
                    lst = decimateDSpec(lineNo, tmp);
                    ret = dSpecRectifier(lst);
                    break;
                case 'I':
                    ret = new List<SyntaxToken>(new SyntaxToken[] { new SyntaxToken(TokenKind.TK_BADTOKEN, lineNo, 0, "", linePos) });
                    break;
                case 'C':
                    lst = decimateCSpec(lineNo, tmp);

                    // ------------------------------------------------------------------------------------------------------------------------------------
                    // C  N01++++
                    // handle conditinal idicatiors for columns 1-8 
                    if (lst[0].symbol != "" || lst[1].symbol != "" || lst[2].symbol != "")
                    {
                        // at the start of indicator control insert an if block
                        if (localTokenLst2.Count == 0)
                            localTokenLst2.Add(new SyntaxToken(TokenKind.TK_IF, lst[0].linePos, computeCharPos(lst[0].chrPos), "", lst[0].chrPos));

                        // add a AND/ OR token if needed
                        switch (lst[0].symbol)
                        {
                            case "AN":
                                localTokenLst2.Add(new SyntaxToken(TokenKind.TK_AND, lst[0].linePos, computeCharPos(lst[0].chrPos), "AN", lst[0].chrPos));
                                break;
                            case "OR":
                                localTokenLst2.Add(new SyntaxToken(TokenKind.TK_OR, lst[0].linePos, computeCharPos(lst[0].chrPos), "OR", lst[0].chrPos));
                                break;
                            case "":
                                localTokenLst2.Add(new SyntaxToken(TokenKind.TK_SPACE, lst[0].linePos, computeCharPos(lst[0].chrPos), "", lst[0].chrPos));
                                break;
                            default:
                                localTokenLst2.Add(new SyntaxToken(TokenKind.TK_BADTOKEN, lst[0].linePos, computeCharPos(lst[0].chrPos), "", lst[0].chrPos));
                                break;
                        }
                        pos += 2;

                        // add NOT if needed
                        switch (lst[1].symbol)
                        {
                            case "N":
                                localTokenLst2.Add(new SyntaxToken(TokenKind.TK_NOT, lst[0].linePos, computeCharPos(lst[1].chrPos), "N", lst[0].chrPos));
                                break;
                            case "":
                                localTokenLst2.Add(new SyntaxToken(TokenKind.TK_SPACE, lst[0].linePos, computeCharPos(lst[1].chrPos), "", lst[0].chrPos));
                                break;
                            default:
                                localTokenLst2.Add(new SyntaxToken(TokenKind.TK_BADTOKEN, lst[0].linePos, computeCharPos(lst[1].chrPos), "", lst[0].chrPos));
                                break;
                        }
                        pos += 1;

                        // Add Indicator boolean logic
                        tmp = SyntaxFacts.getAllIndicators().Where(ik => ik == lst[2].symbol).FirstOrDefault();
                        if (tmp != null)
                        {
                            lst[2].symbol = $"*IN{lst[2].symbol}";
                            localTokenLst2.AddRange(doLex(lst[2]));
                            localTokenLst2.Add(new SyntaxToken(TokenKind.TK_EQ, lst[2].linePos, computeCharPos(lst[2].chrPos), "", lst[2].chrPos));
                            localTokenLst2.Add(new SyntaxToken(TokenKind.TK_INDOFF, lst[2].linePos, computeCharPos(lst[2].chrPos), "", lst[2].chrPos));
                        }

                        // return blank list this will request another card
                        if (lst[4].symbol == "")
                            return new List<SyntaxToken>(new SyntaxToken[] { new SyntaxToken(TokenKind.TK_BLOCKSTART, 0, 0, "", "!__ReqCard__", linePos) });
                        else
                        {
                            // compleate hidden goto statement
                            localTokenLst2.Add(new SyntaxToken(TokenKind.TK_BLOCKSTART, lst[0].linePos, lst[0].chrPos, "", lst[0].chrPos));
                            localTokenLst2.Add(new SyntaxToken(TokenKind.TK_GOTO, lst[0].linePos, lst[0].chrPos, "", lst[0].chrPos));
                            localTokenLst2.Add(new SyntaxToken(TokenKind.TK_IDENTIFIER, lst[0].linePos, lst[0].chrPos, $"^^ITag{ITagCnt}", lst[0].chrPos));
                            localTokenLst2.Add(new SyntaxToken(TokenKind.TK_ENDIF, lst[0].linePos, lst[0].chrPos, "", lst[0].chrPos));
                            ITagCnt += 1;
                        }
                    }
                    // ------------------------------------------------------------------------------------------------------------------------------
                    // handle multiline RPG conditinals (ANDEQ, ORGT, ORNE)
                    if (SyntaxFacts.doColectAnotherCard(lst[4].symbol) == true)
                    {
                        localTokenLst.AddRange(cSpecRectifier(lst));

                        // colect tokens for this line
                        if (localTokenLst[localTokenLst.Count - 1].kind == TokenKind.TK_BLOCKSTART)
                            localTokenLst.RemoveAt(localTokenLst.Count - 1);

                        // return blank list this will request another card
                        return new List<SyntaxToken>(new SyntaxToken[] { new SyntaxToken(TokenKind.TK_BLOCKSTART, 0, 0, "", "!__ReqCard__") });
                    }
                    if (localTokenLst.Count > 0)
                    {
                        // move local tokens to returning list 
                        // then reset for next run
                        ret = new List<SyntaxToken>(localTokenLst);
                        localTokenLst.Clear();

                        // add block start token
                        ret.Add(new SyntaxToken(TokenKind.TK_BLOCKSTART, ret[ret.Count - 1].line, computeCharPos(ret[ret.Count - 1].pos), ""));
                        ret.AddRange(cSpecRectifier(lst));
                        return ret;
                    }

                    // ------------------------------------------------------------------------------------------------------------------------------
                    // do standard lex
                    ret = cSpecRectifier(lst);
                    break;
                case 'O':
                    ret = new List<SyntaxToken>(new SyntaxToken[] { new SyntaxToken(TokenKind.TK_BADTOKEN, lineNo, 0, "", linePos) });
                    break;
                case 'P':
                    ret = new List<SyntaxToken>(new SyntaxToken[] { new SyntaxToken(TokenKind.TK_BADTOKEN, lineNo, 0, "", linePos) });
                    break;
                default:
                    ret = new List<SyntaxToken>(new SyntaxToken[] { new SyntaxToken(TokenKind.TK_BADTOKEN, lineNo, 0, "", linePos) });
                    break;
            }


            // return lex tokens
            return ret;
        }

        // ////////////////////////////////////////////////////////////////////////////
        public static List<SyntaxToken> doDecimation3(List<StructCard> cards, ref DiagnosticBag diag)
        {
            bool hasEndToken;
            char Specification;
            string tmp, line;
            int charPos, tn, lineNo;
            string peekOp, cascadeOp = null;
            List<StructNode> lst;
            List<SyntaxToken> ret;
            StructCard card;

            ret = new List<SyntaxToken>();

            // setup global diagnostic bag
            diagnostics = diag;

            for (int i = 0; i < cards.Count(); i++)
            {
                card = cards[i];
                line = card.Line;
                charPos = 0;
                lineNo = card.LinePos;

                // line is to short to use, go to next line
                if (line.Length < 3)
                    continue;

                // setup line
                tmp = line.PadRight(72);
                Specification = line[0];
                lineStart = charPos;

                // end of file line
                if (line[0] == '\0')
                {
                    ret.Add(new SyntaxToken(TokenKind.TK_EOI, lineNo, charPos, ""));
                    continue;
                }

                // do not try to decimate a blank line
                if (tmp[0] == ' ')
                {
                    ret.Add(new SyntaxToken(TokenKind.TK_SPACE, lineNo, 0, ""));
                    continue;
                }

                // handle comments
                if (tmp[1] == '*' || (tmp[1] == '/' && tmp[2] == '/'))
                {
                    ret.Add(new SyntaxToken(TokenKind.TK_SPACE, lineNo, 0, ""));
                    continue;
                }


                // begin decimation
                switch (Specification)
                {
                    case 'H':
                        lst = decimateHSpec(lineNo, tmp);
                        ret.Add(new SyntaxToken(TokenKind.TK_BADTOKEN, lst[0].linePos, lst[0].chrPos, "", lst[0].chrPos));
                        break;
                    case 'F':
                        lst = decimateFSpec(lineNo, tmp);
                        ret.Add(new SyntaxToken(TokenKind.TK_BADTOKEN, lst[0].linePos, lst[0].chrPos, "", lst[0].chrPos));
                        break;
                    case 'D':
                        lst = decimateDSpec(lineNo, tmp);
                        ret.AddRange(dSpecRectifier(lst));
                        break;
                    case 'I':
                        ret.Add(new SyntaxToken(TokenKind.TK_BADTOKEN, linePos, 0, "", 1));
                        break;
                    case 'C':
                        lst = decimateCSpec(lineNo, tmp);

                        // peek ahead
                        if ((i + 1) < cards.Count())
                        {
                            line = cards[i + 1].Line;

                            if (line.Length > 30)
                                peekOp = line.Substring(20, 10).Trim();
                            else
                                peekOp = "";
                        }
                        else
                            peekOp = "";

                        // ------------------------------------------------------------------------------------------------------------------------------------
                        // C  N01++++
                        // handle conditinal idicatiors for columns 1-8 
                        if (lst[0].symbol != "" || lst[1].symbol != "" || lst[2].symbol != "")
                        {
                            // at the start of indicator control insert an if block
                            if (localTokenLst.Count == 0)
                                localTokenLst.Add(new SyntaxToken(TokenKind.TK_IF, lst[0].linePos, computeCharPos(lst[0].chrPos), "", lst[0].chrPos));

                            // add a AND/ OR token if needed
                            switch (lst[0].symbol)
                            {
                                case "AN":
                                    localTokenLst.Add(new SyntaxToken(TokenKind.TK_AND, lst[0].linePos, computeCharPos(lst[0].chrPos), "AN", lst[0].chrPos));
                                    break;
                                case "OR":
                                    localTokenLst.Add(new SyntaxToken(TokenKind.TK_OR, lst[0].linePos, computeCharPos(lst[0].chrPos), "OR", lst[0].chrPos));
                                    break;
                                case "":
                                    localTokenLst.Add(new SyntaxToken(TokenKind.TK_SPACE, lst[0].linePos, computeCharPos(lst[0].chrPos), "", lst[0].chrPos));
                                    break;
                                default:
                                    localTokenLst.Add(new SyntaxToken(TokenKind.TK_BADTOKEN, lst[0].linePos, computeCharPos(lst[0].chrPos), "", lst[0].chrPos));
                                    break;
                            }

                            // add NOT if needed
                            switch (lst[1].symbol)
                            {
                                case "N":
                                    localTokenLst.Add(new SyntaxToken(TokenKind.TK_NOT, lst[0].linePos, computeCharPos(lst[1].chrPos), "N", lst[0].chrPos));
                                    break;
                                case "":
                                    localTokenLst.Add(new SyntaxToken(TokenKind.TK_SPACE, lst[0].linePos, computeCharPos(lst[1].chrPos), "", lst[0].chrPos));
                                    break;
                                default:
                                    localTokenLst.Add(new SyntaxToken(TokenKind.TK_BADTOKEN, lst[0].linePos, computeCharPos(lst[1].chrPos), "", lst[0].chrPos));
                                    break;
                            }

                            // Add Indicator boolean logic
                            tmp = SyntaxFacts.getAllIndicators().Where(ik => ik == lst[2].symbol).FirstOrDefault();
                            if (tmp != null)
                            {
                                lst[2].symbol = $"*IN{lst[2].symbol}";
                                localTokenLst.AddRange(doLex(lst[2]));
                                localTokenLst.Add(new SyntaxToken(TokenKind.TK_EQ, lst[2].linePos, computeCharPos(lst[2].chrPos), "", lst[2].chrPos));
                                localTokenLst.Add(new SyntaxToken(TokenKind.TK_INDOFF, lst[2].linePos, computeCharPos(lst[2].chrPos), "", lst[2].chrPos));
                            }

                            // there is no op-code on this line go to next line and repeate this process
                            if (lst[4].symbol == "")
                                continue;
                            else
                            {
                                // compleate hidden goto statement
                                localTokenLst.Add(new SyntaxToken(TokenKind.TK_NEWLINE, lst[0].linePos, lst[0].chrPos, "", lst[0].chrPos));
                                localTokenLst.Add(new SyntaxToken(TokenKind.TK_BLOCKSTART, lst[0].linePos, lst[0].chrPos, "", lst[0].chrPos));
                                localTokenLst.Add(new SyntaxToken(TokenKind.TK_GOTO, lst[0].linePos, lst[0].chrPos, "", lst[0].chrPos));
                                localTokenLst.Add(new SyntaxToken(TokenKind.TK_IDENTIFIER, lst[0].linePos, lst[0].chrPos, $"^^ITag{ITagCnt}", lst[0].chrPos));
                                localTokenLst.Add(new SyntaxToken(TokenKind.TK_NEWLINE, lst[0].linePos, lst[0].chrPos, "", lst[0].chrPos));
                                localTokenLst.Add(new SyntaxToken(TokenKind.TK_ENDIF, lst[0].linePos, lst[0].chrPos, "", lst[0].chrPos));
                                localTokenLst.Add(new SyntaxToken(TokenKind.TK_NEWLINE, lst[0].linePos, lst[0].chrPos, "", lst[0].chrPos));

                                // merge tokens to main list
                                ret.AddRange(localTokenLst);
                                // add current line to list
                                ret.AddRange(cSpecRectifier(lst));

                                // add ending tag and clear control indicators
                                tmp = $"^^ITag{ITagCnt}";
                                ret.Add(new SyntaxToken(TokenKind.TK_TAG, lst[0].linePos, computeCharPos(lst[0].chrPos), "TAG", tmp, lst[0].chrPos));
                                ret.Add(new SyntaxToken(TokenKind.TK_IDENTIFIER, lst[0].linePos, computeCharPos(lst[0].chrPos), tmp, lst[0].chrPos));
                                ret.Add(new SyntaxToken(TokenKind.TK_NEWLINE, lst[0].linePos, lst[0].chrPos, "", lst[0].chrPos));
                                localTokenLst.Clear();

                                ITagCnt += 1;
                                continue;
                            }
                        }
                        // ------------------------------------------------------------------------------------------------------------------------------
                        // handle multiline RPG conditinals (ANDEQ, ORGT, ORNE)
                        if (SyntaxFacts.doColectAnotherCard(lst[4].symbol) == true)
                        {
                            ret.AddRange(cSpecRectifier(lst));

                            // set cascade operation symbol [cascadeOp] will have a value only for IF and DO bocks
                            cascadeOp = lst[4].symbol;
                            if (SyntaxFacts.cascadeBlockStart(cascadeOp) == true)
                                cascadeOp = cascadeOp.Substring(0,2);
                            else
                                cascadeOp = "";

                            // add block start token when the next token is not apart of the cascade
                            // this will compleate the if cascade
                            if (SyntaxFacts.doColectAnotherCard(peekOp) == false || SyntaxFacts.cascadeBlockStart(peekOp) == true)
                            {
                                // add block start token if needed
                                if (ret[ret.Count - 1].kind != TokenKind.TK_BLOCKSTART)
                                {
                                    ret.Add(new SyntaxToken(TokenKind.TK_NEWLINE, 0, 0, "", lst[0].chrPos));
                                    ret.Add(new SyntaxToken(TokenKind.TK_BLOCKSTART, 0, 0, cascadeOp, lst[0].chrPos));
                                }
                                cascadeOp = "";
                            }
                            else
                            {
                                // remove start block token
                                if (ret[ret.Count - 1].kind == TokenKind.TK_BLOCKSTART)
                                {
                                    ret.RemoveAt(ret.Count - 1);
                                    ret.RemoveAt(ret.Count - 1);
                                }
                            }

                            continue;
                        }

                        // ------------------------------------------------------------------------------------------------------------------------------
                        // do standard lex
                        ret.AddRange(cSpecRectifier(lst));
                        break;
                    case 'O':
                        ret = new List<SyntaxToken>(new SyntaxToken[] { new SyntaxToken(TokenKind.TK_BADTOKEN, lineNo, 0, "", linePos) });
                        break;
                    case 'P':
                        ret = new List<SyntaxToken>(new SyntaxToken[] { new SyntaxToken(TokenKind.TK_BADTOKEN, lineNo, 0, "", linePos) });
                        break;
                    default:
                        ret = new List<SyntaxToken>(new SyntaxToken[] { new SyntaxToken(TokenKind.TK_BADTOKEN, lineNo, 0, "", linePos) });
                        break;
                }
            }

            // ret has nothing to return move local ist to ret
            if (localTokenLst.Count() > 0)
            {
                ret.AddRange(localTokenLst);
                localTokenLst.Clear();
            }

            // check if end token is in list
            hasEndToken = ret.Select(tkn => tkn.kind == TokenKind.TK_EOI).FirstOrDefault();

            // add end of input token
            if (hasEndToken == false)
            {
                ret.Add(new SyntaxToken(TokenKind.TK_EOI, 0, 0, ""));

                ITagCnt = 0;
            }

            // return lex tokens
            return ret;
        }

        // //////////////////////////////////////////////////////////////////////////////////
        public static List<SyntaxToken> cSpecRectifier(List<StructNode> lst)
        {
            List<SyntaxToken> ret = new List<SyntaxToken>();
            SyntaxToken tToken;
            StructNode snode = null;
            int itmCnt;
            string OpCode, tmp;

            itmCnt = lst.Count;
            onEvalLine = false;
            onBooleanLine = false;

            // factor 1 is not empty and has no key word
            if (itmCnt == 4 && lst[3].symbol.Length > 0)
            {
                ret.Add(new SyntaxToken(TokenKind.TK_BADTOKEN, lst[itmCnt].linePos, lst[itmCnt].chrPos, "", lst[itmCnt].chrPos));
                diagnostics.reportMissingFactor1(new TextSpan(lst[3].chrPos, lst[3].symbol.Length),
                                                 lst[3].chrPos);
            }

            // rectify structured code to free lexicon
            if (itmCnt >= 6)
            {
                // check if the opcode is valid
                OpCode = lst[4].symbol.Trim();
                if (SyntaxFacts.isValidOpCode(OpCode) == false)
                {
                    diagnostics.reportBadOpcode(OpCode, lst[4].linePos, lst[4].chrPos);
                    ret.Add(new SyntaxToken(TokenKind.TK_IDENTIFIER, lst[4].linePos, computeCharPos(lst[4].chrPos), OpCode, lst[4].chrPos));
                    return ret;
                }

                // add control indicators to the output
                if (localTokenLst2.Count > 0)
                    ret.AddRange(localTokenLst2);

                // perform rectifier
                switch (OpCode)
                {
                    case "ADD":
                    case "SUB":
                    case "MULT":
                    case "DIV":
                        snode = leftJustified(lst);
                        if (snode == null)
                        {
                            if (lst[3].symbol == "")
                            {
                                // +=,-=,*=,/= factor 2 to factor 3
                                ret.AddRange(doLex(lst[6]));
                                ret.Add(new SyntaxToken(TokenKind.TK_ASSIGN, lst[6].linePos, computeCharPos(lst[6].chrPos), OpCode, lst[6].chrPos));
                                ret.AddRange(doLex(lst[6]));
                                ret.AddRange(doLex(lst[4]));
                                ret.AddRange(doLex(lst[5]));
                                ret.Add(new SyntaxToken(TokenKind.TK_NEWLINE, lst[4].linePos, computeCharPos(lst[4].chrPos), "", lst[4].chrPos));
                            }
                            else
                            {
                                // factors 1,2 and 3
                                ret.AddRange(doLex(lst[6]));
                                ret.Add(new SyntaxToken(TokenKind.TK_ASSIGN, lst[6].linePos, computeCharPos(lst[6].chrPos), OpCode, lst[6].chrPos));
                                ret.AddRange(doLex(lst[3]));
                                ret.AddRange(doLex(lst[4]));
                                ret.AddRange(doLex(lst[5]));
                                ret.Add(new SyntaxToken(TokenKind.TK_NEWLINE, lst[4].linePos, computeCharPos(lst[4].chrPos), "", lst[4].chrPos));

                                if (OpCode == "DIV")
                                {
                                    ret.Add(new SyntaxToken(TokenKind.TK_IDENTIFIER, lst[4].linePos, computeCharPos(lst[4].chrPos), "^^LO", lst[4].chrPos));
                                    ret.Add(new SyntaxToken(TokenKind.TK_ASSIGN, lst[4].linePos, computeCharPos(lst[4].chrPos), "=", lst[4].chrPos));
                                    snode = new StructNode(0, 0, $"%REM({lst[3].symbol}:{lst[5].symbol})");
                                    ret.AddRange(doLex(snode));
                                    ret.Add(new SyntaxToken(TokenKind.TK_NEWLINE, lst[4].linePos, computeCharPos(lst[4].chrPos), "", lst[4].chrPos));
                                }
                            }
                        }
                        else
                            ret.Add(reportCSpecPositionError(snode));
                        break;
                    case "MVR":
                        snode = leftJustified(lst);
                        if (snode == null)
                        {
                            ret.Add(new SyntaxToken(TokenKind.TK_IDENTIFIER, lst[6].linePos, computeCharPos(lst[6].chrPos), lst[6].symbol, lst[6].chrPos));
                            ret.Add(new SyntaxToken(TokenKind.TK_ASSIGN, lst[4].linePos, computeCharPos(lst[4].chrPos), "=", lst[4].chrPos));
                            ret.Add(new SyntaxToken(TokenKind.TK_IDENTIFIER, lst[4].linePos, computeCharPos(lst[4].chrPos), "^^LO", lst[4].chrPos));
                            ret.Add(new SyntaxToken(TokenKind.TK_NEWLINE, lst[4].linePos, computeCharPos(lst[4].chrPos), "", lst[4].chrPos));
                        }
                        else
                            ret.Add(reportCSpecPositionError(snode));
                        break;
                    case "CALLB":
                    case "CALLP":
                        onBooleanLine = true;
                        break;
                    case "COMP":
                        snode = leftJustified(lst);
                        if (snode == null)
                        {
                            ret.AddRange(doLex(getComparisonInd(lst[9], lst[10], lst[11])));
                            ret.Add(new SyntaxToken(TokenKind.TK_ASSIGN, lst[4].linePos, computeCharPos(lst[4].chrPos), "COMP", lst[4].chrPos));
                            ret.AddRange(doLex(lst[3]));
                            ret.Add(new SyntaxToken(SyntaxFacts.getindicatorOperation(lst[9].symbol, lst[10].symbol, lst[11].symbol), lst[4].linePos, computeCharPos(lst[4].chrPos), OpCode, lst[4].chrPos));
                            ret.AddRange(doLex(lst[5]));
                            ret.Add(new SyntaxToken(TokenKind.TK_NEWLINE, lst[4].linePos, computeCharPos(lst[4].chrPos), "", lst[4].chrPos));
                        }
                        else;
                        ret.Add(reportCSpecPositionError(snode));
                        break;
                    case "CIN":
                        snode = leftJustified(lst);
                        if (snode == null)
                        {
                            //ret.AddRange(doLex(lst[6]));
                            ret.AddRange(doLex(lst[3]));
                            ret.Add(new SyntaxToken(TokenKind.TK_ASSIGN, lst[6].linePos, computeCharPos(lst[6].chrPos), "", lst[6].chrPos));
                            ret.Add(new SyntaxToken(TokenKind.TK_IDENTIFIER, lst[4].linePos, computeCharPos(lst[4].chrPos), OpCode, lst[4].chrPos));
                            ret.Add(new SyntaxToken(TokenKind.TK_PARENOPEN, lst[4].linePos, computeCharPos(lst[4].chrPos), "", lst[4].chrPos));
                            ret.Add(new SyntaxToken(TokenKind.TK_PARENCLOSE, lst[4].linePos, computeCharPos(lst[4].chrPos), "", lst[4].chrPos));
                            ret.Add(new SyntaxToken(TokenKind.TK_NEWLINE, lst[4].linePos, computeCharPos(lst[4].chrPos), "", lst[4].chrPos));
                        }
                        else;
                        ret.Add(reportCSpecPositionError(snode));
                        break;
                    case "COUT":
                    case "PRINT":
                    case "DSPLY":
                        snode = leftJustified(lst);
                        if (snode == null)
                        {
                            ret.Add(new SyntaxToken(TokenKind.TK_IDENTIFIER, lst[4].linePos, computeCharPos(lst[4].chrPos), OpCode, lst[4].chrPos));
                            ret.AddRange(doLex(lst[3]));
                            ret.Add(new SyntaxToken(TokenKind.TK_NEWLINE, lst[4].linePos, computeCharPos(lst[4].chrPos), "", lst[4].chrPos));
                        }
                        else
                            ret.Add(reportCSpecPositionError(snode));
                        break;
                    case "ITER":
                        ret.Add(new SyntaxToken(TokenKind.TK_ITER, lst[4].linePos, lst[4].chrPos, OpCode, lst[4].chrPos));
                        ret.Add(new SyntaxToken(TokenKind.TK_NEWLINE, lst[4].linePos, lst[4].chrPos, "", lst[4].chrPos));
                        break;
                    case "LEAVE":
                        ret.Add(new SyntaxToken(TokenKind.TK_LEAVE, lst[4].linePos, lst[4].chrPos, OpCode, lst[4].chrPos));
                        ret.Add(new SyntaxToken(TokenKind.TK_NEWLINE, lst[4].linePos, lst[4].chrPos, "", lst[4].chrPos));
                        break;
                    case "DO":
                        ret.Add(new SyntaxToken(TokenKind.TK_BLOCKSTART, lst[4].linePos, computeCharPos(lst[4].chrPos), OpCode, lst[4].chrPos));
                        break;
                    case "DOUGE":
                    case "DOUGT":
                    case "DOULE":
                    case "DOULT":
                    case "DOUEQ":
                    case "DOUNE":
                        snode = leftJustified(lst);
                        if (snode == null)
                        {
                            ret.Add(new SyntaxToken(TokenKind.TK_DOU, lst[4].linePos, computeCharPos(lst[4].chrPos), OpCode, lst[4].chrPos));
                            ret.AddRange(doLex(lst[3]));
                            ret.AddRange(doLex(lst[4]));
                            ret.AddRange(doLex(lst[5]));
                            ret.Add(new SyntaxToken(TokenKind.TK_NEWLINE, lst[4].linePos, computeCharPos(lst[4].chrPos), "", lst[4].chrPos));
                            ret.Add(new SyntaxToken(TokenKind.TK_BLOCKSTART, lst[4].linePos, computeCharPos(lst[4].chrPos), "DO", lst[4].chrPos));
                            lineType = "DOU";
                        }
                        else
                            ret.Add(reportCSpecPositionError(snode));
                        break;
                    case "DOU":
                        snode = leftJustified(lst);
                        if (snode == null)
                        {
                            onBooleanLine = true;
                            if (lst[3].symbol != "")
                            {
                                // somthing was entered in factor
                                ret.Add(new SyntaxToken(TokenKind.TK_SPACE, lst[3].linePos, computeCharPos(lst[3].chrPos), "", lst[4].chrPos));
                                diagnostics.reportBadFactor(new TextSpan(lst[3].chrPos, lst[3].symbol.Length), 1, lst[3].chrPos);
                            }
                            else
                            {
                                ret.Add(new SyntaxToken(TokenKind.TK_DOU, lst[4].linePos, computeCharPos(lst[4].chrPos), OpCode, lst[4].chrPos));
                                ret.AddRange(doLex(lst[5]));
                                ret.Add(new SyntaxToken(TokenKind.TK_NEWLINE, lst[4].linePos, computeCharPos(lst[4].chrPos), "", lst[4].chrPos));
                                ret.Add(new SyntaxToken(TokenKind.TK_BLOCKSTART, lst[4].linePos, computeCharPos(lst[4].chrPos), "DO", lst[4].chrPos));
                                lineType = "DOU";
                            }
                        }
                        else
                            ret.Add(reportCSpecPositionError(snode));
                        break;
                    case "DOWGE":
                    case "DOWGT":
                    case "DOWLE":
                    case "DOWLT":
                    case "DOWEQ":
                    case "DOWNE":
                        snode = leftJustified(lst);
                        if (snode == null)
                        {
                            ret.Add(new SyntaxToken(TokenKind.TK_DOW, lst[4].linePos, computeCharPos(lst[4].chrPos), OpCode, lst[4].chrPos));
                            ret.AddRange(doLex(lst[3]));
                            ret.AddRange(doLex(lst[4]));
                            ret.AddRange(doLex(lst[5]));
                            ret.Add(new SyntaxToken(TokenKind.TK_NEWLINE, lst[4].linePos, computeCharPos(lst[4].chrPos), "", lst[4].chrPos));
                            ret.Add(new SyntaxToken(TokenKind.TK_BLOCKSTART, lst[4].linePos, computeCharPos(lst[4].chrPos), "DO", lst[4].chrPos));
                            lineType = "DOW";
                        }
                        else
                            ret.Add(reportCSpecPositionError(snode));
                        break;
                    case "DOW":
                        snode = leftJustified(lst);
                        if (snode == null)
                        {
                            onBooleanLine = true;
                            if (lst[3].symbol != "")
                            {
                                // somthing was entered in factor 1
                                ret.Add(new SyntaxToken(TokenKind.TK_SPACE, lst[3].linePos, computeCharPos(lst[3].chrPos), "", lst[3].chrPos));
                                diagnostics.reportBadFactor(new TextSpan(lst[3].chrPos, lst[3].symbol.Length), 1, lst[3].chrPos);
                            }
                            else
                            {
                                ret.Add(new SyntaxToken(TokenKind.TK_DOW, lst[4].linePos, computeCharPos(lst[4].chrPos), OpCode, lst[4].chrPos));
                                ret.AddRange(doLex(lst[5]));
                                ret.Add(new SyntaxToken(TokenKind.TK_NEWLINE, lst[4].linePos, computeCharPos(lst[4].chrPos), "", lst[4].chrPos));
                                ret.Add(new SyntaxToken(TokenKind.TK_BLOCKSTART, lst[4].linePos, computeCharPos(lst[4].chrPos), "DO", lst[4].chrPos));
                                lineType = "DOW";
                            }
                        }
                        else
                            ret.Add(reportCSpecPositionError(snode));
                        break;
                    case "IF":
                        snode = leftJustified(lst);
                        if (snode == null)
                        {
                            onBooleanLine = true;
                            if (lst[3].symbol != "")
                            {
                                // somthing was entered in factor
                                ret.Add(new SyntaxToken(TokenKind.TK_SPACE, lst[3].linePos, computeCharPos(lst[3].chrPos), "", lst[3].chrPos));
                                diagnostics.reportBadFactor(new TextSpan(lst[3].chrPos, lst[3].symbol.Length), 1, lst[3].chrPos);
                            }
                            else
                            {
                                ret.Add(new SyntaxToken(TokenKind.TK_IF, lst[4].linePos, computeCharPos(lst[4].chrPos), OpCode, lst[4].chrPos));
                                ret.AddRange(doLex(lst[5]));
                                ret.Add(new SyntaxToken(TokenKind.TK_NEWLINE, lst[4].linePos, computeCharPos(lst[4].chrPos), "", lst[4].chrPos));
                                ret.Add(new SyntaxToken(TokenKind.TK_BLOCKSTART, lst[4].linePos, computeCharPos(lst[4].chrPos), "IF", lst[4].chrPos));
                                lineType = "IF";
                            }
                        }
                        else
                            ret.Add(reportCSpecPositionError(snode));
                        break;
                    case "IFGE":
                    case "IFGT":
                    case "IFLE":
                    case "IFLT":
                    case "IFEQ":
                    case "IFNE":
                        snode = leftJustified(lst);
                        if (snode == null)
                        {
                            ret.Add(new SyntaxToken(TokenKind.TK_IF, lst[4].linePos, computeCharPos(lst[4].chrPos), OpCode, lst[4].chrPos));
                            ret.AddRange(doLex(lst[3]));
                            ret.AddRange(doLex(lst[4]));
                            ret.AddRange(doLex(lst[5]));
                            ret.Add(new SyntaxToken(TokenKind.TK_NEWLINE, lst[4].linePos, computeCharPos(lst[4].chrPos), "", lst[4].chrPos));
                            ret.Add(new SyntaxToken(TokenKind.TK_BLOCKSTART, lst[4].linePos, computeCharPos(lst[4].chrPos), "IF", lst[4].chrPos));
                            lineType = "IF";
                        }
                        else
                            ret.Add(reportCSpecPositionError(snode));
                        break;
                    case "ELSE":
                        snode = leftJustified(lst);
                        if (snode == null)
                        {
                            ret.Add(new SyntaxToken(TokenKind.TK_ELSE, lst[4].linePos, computeCharPos(lst[4].chrPos), OpCode, lst[4].chrPos));
                            ret.Add(new SyntaxToken(TokenKind.TK_NEWLINE, lst[4].linePos, computeCharPos(lst[4].chrPos), "", lst[4].chrPos));
                            ret.Add(new SyntaxToken(TokenKind.TK_BLOCKSTART, lst[4].linePos, computeCharPos(lst[4].chrPos), "IF", lst[4].chrPos));
                            lineType = "ELSE";
                        }
                        else
                            ret.Add(reportCSpecPositionError(snode));
                        break;
                    case "END":
                        snode = leftJustified(lst);
                        if (snode == null)
                        {
                            ret.Add(new SyntaxToken(TokenKind.TK_BLOCKEND, lst[4].linePos, computeCharPos(lst[4].chrPos), OpCode, lst[4].chrPos));
                            ret.Add(new SyntaxToken(TokenKind.TK_NEWLINE, lst[4].linePos, computeCharPos(lst[4].chrPos), "", lst[4].chrPos));
                            lineType = "";
                        }
                        else
                            ret.Add(reportCSpecPositionError(snode));
                        break;
                    case "ENDDO":
                        snode = leftJustified(lst);
                        if (snode == null)
                        {
                            ret.Add(new SyntaxToken(TokenKind.TK_ENDDO, lst[4].linePos, computeCharPos(lst[4].chrPos), OpCode, lst[4].chrPos));
                            ret.Add(new SyntaxToken(TokenKind.TK_NEWLINE, lst[4].linePos, computeCharPos(lst[4].chrPos), "", lst[4].chrPos));
                            lineType = "";
                        }
                        else
                            ret.Add(reportCSpecPositionError(snode));
                        break;
                    case "ENDIF":
                        snode = leftJustified(lst);
                        if (snode == null)
                        {
                            ret.Add(new SyntaxToken(TokenKind.TK_ENDIF, lst[4].linePos, computeCharPos(lst[4].chrPos), OpCode, lst[4].chrPos));
                            ret.Add(new SyntaxToken(TokenKind.TK_NEWLINE, lst[4].linePos, computeCharPos(lst[4].chrPos), "", lst[4].chrPos));
                            lineType = "";
                        }
                        else
                            ret.Add(reportCSpecPositionError(snode));
                        break;
                    case "ENDFOR":
                        snode = leftJustified(lst);
                        if (snode == null)
                        {
                            ret.Add(new SyntaxToken(TokenKind.TK_ENDFOR, lst[4].linePos, computeCharPos(lst[4].chrPos), OpCode, lst[4].chrPos));
                            ret.Add(new SyntaxToken(TokenKind.TK_NEWLINE, lst[4].linePos, computeCharPos(lst[4].chrPos), "", lst[4].chrPos));
                            lineType = "";
                        }
                        else
                            ret.Add(reportCSpecPositionError(snode));
                        break;
                    case "ENDMON":
                        snode = leftJustified(lst);
                        if (snode == null)
                        {
                            ret.Add(new SyntaxToken(TokenKind.TK_ENDMON, lst[4].linePos, computeCharPos(lst[4].chrPos), OpCode, lst[4].chrPos));
                            ret.Add(new SyntaxToken(TokenKind.TK_NEWLINE, lst[4].linePos, computeCharPos(lst[4].chrPos), "", lst[4].chrPos));
                            lineType = "";
                        }
                        else
                            ret.Add(reportCSpecPositionError(snode));
                        break;
                    case "ENDSL":
                        snode = leftJustified(lst);
                        if (snode == null)
                        {
                            ret.Add(new SyntaxToken(TokenKind.TK_ENDSL, lst[4].linePos, computeCharPos(lst[4].chrPos), OpCode, lst[4].chrPos));
                            ret.Add(new SyntaxToken(TokenKind.TK_NEWLINE, lst[4].linePos, computeCharPos(lst[4].chrPos), "", lst[4].chrPos));
                            lineType = "";
                        }
                        else
                            ret.Add(reportCSpecPositionError(snode));
                        break;
                    case "ENDSR":
                        snode = leftJustified(lst);
                        if (snode == null)
                        {
                            ret.Add(new SyntaxToken(TokenKind.TK_ENDSR, lst[4].linePos, computeCharPos(lst[4].chrPos), OpCode, lst[4].chrPos));
                            ret.Add(new SyntaxToken(TokenKind.TK_NEWLINE, lst[4].linePos, computeCharPos(lst[4].chrPos), "", lst[4].chrPos));
                            lineType = "";
                        }
                        else
                            ret.Add(reportCSpecPositionError(snode));
                        break;
                    case "ORGE":
                    case "ORGT":
                    case "ORLE":
                    case "ORLT":
                    case "OREQ":
                    case "ORNE":
                        snode = leftJustified(lst);
                        if (snode == null)
                        {
                            ret.Add(new SyntaxToken(TokenKind.TK_OR, lst[4].linePos, computeCharPos(lst[4].chrPos), OpCode, lst[4].chrPos));
                            ret.AddRange(doLex(lst[3]));
                            ret.Add(getComparisonOpCode(lst[4]));
                            ret.AddRange(doLex(lst[5]));
                        }
                        else
                            ret.Add(reportCSpecPositionError(snode));
                        break;
                    case "ANDGE":
                    case "ANDGT":
                    case "ANDLE":
                    case "ANDLT":
                    case "ANDEQ":
                    case "ANDNE":
                        snode = leftJustified(lst);
                        if (snode == null)
                        {
                            ret.Add(new SyntaxToken(TokenKind.TK_AND, lst[4].linePos, computeCharPos(lst[4].chrPos), OpCode, lst[4].chrPos));
                            ret.AddRange(doLex(lst[3]));
                            ret.Add(getComparisonOpCode(lst[4]));
                            ret.AddRange(doLex(lst[5]));
                        }
                        else
                            ret.Add(reportCSpecPositionError(snode));
                        break;
                    case "EVAL":
                    case "EVALR":
                        snode = leftJustified(lst);
                        if (snode == null)
                        {
                            onEvalLine = true;
                            if (lst[3].symbol != "")
                            {
                                // somthing was entered in factor
                                ret.Add(new SyntaxToken(TokenKind.TK_SPACE, lst[3].linePos, computeCharPos(lst[3].chrPos), "", lst[3].chrPos));
                                diagnostics.reportBadFactor(new TextSpan(lst[3].chrPos, lst[3].symbol.Length), 1, lst[3].chrPos);
                            }
                            else
                            {
                                ret.AddRange(doLex(lst[5]));
                                ret.Add(new SyntaxToken(TokenKind.TK_NEWLINE, lst[4].linePos, computeCharPos(lst[4].chrPos), "", lst[4].chrPos));
                            }
                        }
                        else
                            ret.Add(reportCSpecPositionError(snode));
                        break;
                    case "FOR":
                        snode = leftJustified(lst);
                        if (snode == null)
                        {
                            onEvalLine = true;
                            if (lst[3].symbol != "")
                            {
                                // somthing was entered in factor
                                ret.Add(new SyntaxToken(TokenKind.TK_SPACE, lst[3].linePos, computeCharPos(lst[3].chrPos), "", lst[3].chrPos));
                                diagnostics.reportBadFactor(new TextSpan(lst[3].chrPos, lst[3].symbol.Length), 1, lst[3].chrPos);
                            }
                            else
                            {
                                ret.AddRange(doLex(lst[4]));
                                ret.AddRange(doLex(lst[5]));
                                ret.Add(new SyntaxToken(TokenKind.TK_NEWLINE, lst[4].linePos, computeCharPos(lst[4].chrPos), "", lst[4].chrPos));
                                ret.Add(new SyntaxToken(TokenKind.TK_BLOCKSTART, lst[4].linePos, computeCharPos(lst[4].chrPos), "", lst[4].chrPos));
                            }
                        }
                        else
                            ret.Add(reportCSpecPositionError(snode));
                        break;
                    case "MOVE":
                        snode = leftJustified(lst);
                        if (snode == null)
                        {
                            ret.AddRange(doLex(lst[6]));
                            ret.Add(new SyntaxToken(TokenKind.TK_ASSIGN, lst[6].linePos, computeCharPos(lst[4].chrPos), "MOVE", lst[6].chrPos));
                            ret.AddRange(doLex(lst[5]));
                            ret.Add(new SyntaxToken(TokenKind.TK_NEWLINE, lst[4].linePos, computeCharPos(lst[4].chrPos), "", lst[4].chrPos));
                        }
                        else
                            ret.Add(reportCSpecPositionError(snode));
                        break;
                    case "SETON":
                    case "SETOFF":
                        snode = leftJustified(lst);
                        if (snode == null)
                        {
                            for (int i = 0; i < 3; i++)
                            {
                                tmp = lst[9 + i].symbol;
                                if (tmp == "")
                                    continue;

                                lst[9 + i].symbol = $"*IN{tmp}";
                                ret.AddRange(doLex(lst[9 + i]));
                                ret.Add(new SyntaxToken(TokenKind.TK_ASSIGN, lst[4].linePos, computeCharPos(lst[4].chrPos), "", lst[4].chrPos));
                                ret.Add(new SyntaxToken(
                                    ((lst[4].symbol == "SETON") ? TokenKind.TK_INDON : TokenKind.TK_INDOFF),
                                    lst[4].linePos,
                                    computeCharPos(lst[4].chrPos),
                                    "", lst[4].chrPos));
                            }
                            ret.Add(new SyntaxToken(TokenKind.TK_NEWLINE, lst[0].linePos, lst[0].chrPos, ""));
                        }
                        else
                            ret.Add(reportCSpecPositionError(snode));
                        break;
                    case "TAG":
                        snode = leftJustified(lst);
                        if (snode == null)
                        {
                            tToken = new SyntaxToken(TokenKind.TK_TAG, lst[4].linePos, computeCharPos(lst[4].chrPos), OpCode, lst[3].symbol, lst[4].chrPos);
                            ret.Add(tToken);
                            ret.AddRange(doLex(lst[3]));
                            ret.Add(new SyntaxToken(TokenKind.TK_NEWLINE, lst[4].linePos, computeCharPos(lst[4].chrPos), "", lst[4].chrPos));
                        }
                        else
                            ret.Add(reportCSpecPositionError(snode));
                        break;
                    case "GOTO":
                        snode = leftJustified(lst);
                        if (snode == null)
                        {
                            ret.Add(new SyntaxToken(TokenKind.TK_GOTO, lst[4].linePos, computeCharPos(lst[4].chrPos), OpCode, lst[4].chrPos));
                            ret.AddRange(doLex(lst[5]));
                            ret.Add(new SyntaxToken(TokenKind.TK_NEWLINE, lst[4].linePos, computeCharPos(lst[4].chrPos), "", lst[4].chrPos));
                        }
                        else
                            ret.Add(reportCSpecPositionError(snode));
                        break;
                    case "Z-ADD":
                        snode = leftJustified(lst);
                        if (snode == null)
                        {
                            ret.AddRange(doLex(lst[6]));
                            ret.Add(new SyntaxToken(TokenKind.TK_ASSIGN, lst[6].linePos, computeCharPos(lst[4].chrPos), OpCode, lst[6].chrPos));
                            ret.Add(new SyntaxToken(TokenKind.TK_INTEGER, lst[4].linePos, computeCharPos(lst[4].chrPos), "0", lst[4].chrPos));
                            ret.Add(new SyntaxToken(TokenKind.TK_ADD, lst[4].linePos, computeCharPos(lst[4].chrPos), OpCode, lst[4].chrPos));
                            ret.AddRange(doLex(lst[5]));
                            ret.Add(new SyntaxToken(TokenKind.TK_NEWLINE, lst[4].linePos, computeCharPos(lst[4].chrPos), "", lst[4].chrPos));
                        }
                        else
                            ret.Add(reportCSpecPositionError(snode));
                        break;
                    case "Z-SUB":
                        snode = leftJustified(lst);
                        if (snode == null)
                        {
                            ret.AddRange(doLex(lst[6]));
                            ret.Add(new SyntaxToken(TokenKind.TK_ASSIGN, lst[6].linePos, computeCharPos(lst[4].chrPos), OpCode, lst[6].chrPos));
                            ret.AddRange(doLex(new StructNode(lst[4].linePos, computeCharPos(lst[4].chrPos), "0")));
                            ret.Add(new SyntaxToken(TokenKind.TK_SUB, lst[4].linePos, computeCharPos(lst[4].chrPos), OpCode, lst[4].chrPos));
                            ret.AddRange(doLex(lst[5]));
                            ret.Add(new SyntaxToken(TokenKind.TK_NEWLINE, lst[4].linePos, computeCharPos(lst[4].chrPos), "", lst[4].chrPos));
                        }
                        else
                            ret.Add(reportCSpecPositionError(snode));
                        break;
                }
            }

            // add cSpec variable Declaration
            if (localCSpecDclr.Count > 0)
            {
                localCSpecDclr.AddRange(ret);
                ret = new List<SyntaxToken>(localCSpecDclr);
                localCSpecDclr.Clear();
            }

            return ret;
        }

        // //////////////////////////////////////////////////////////////////////////////////
        public static List<SyntaxToken> dSpecRectifier(List<StructNode> lst)
        {
            List<SyntaxToken> ret = new List<SyntaxToken>();
            string dclType;

            dclType = lst[3].symbol;

            switch (dclType)
            {
                case "C":
                    ret.Add(new SyntaxToken(TokenKind.TK_VARDCONST, computeCharPos(lst[0].linePos), 1, "C", lst[3].chrPos));
                    ret.Add(new SyntaxToken(TokenKind.TK_IDENTIFIER, lst[0].linePos, computeCharPos(lst[0].chrPos), lst[0].symbol, lst[0].chrPos));
                    //ret.Add(new SyntaxToken(SyntaxFacts.getRPGType(lst[6].symbol), computeCharPos(lst[6].linePos), lst[6].chrPos, lst[6].symbol, lst[6].chrPos));
                    ret.Add(new SyntaxToken(TokenKind.TK_NEWLINE, computeCharPos(lst[0].linePos), 0, "", lst[0].chrPos));
                    break;
                case "DS":
                    break;
                case "PI":
                    break;
                case "PR":
                    break;
                case "S":
                default:
                    ret.Add(new SyntaxToken(TokenKind.TK_VARDECLR, computeCharPos(lst[0].linePos), 1, "S", lst[3].chrPos));
                    ret.Add(new SyntaxToken(TokenKind.TK_IDENTIFIER, lst[0].linePos, computeCharPos(lst[0].chrPos), lst[0].symbol, lst[1].chrPos));
                    ret.Add(new SyntaxToken(TokenKind.TK_IDENTIFIER, lst[6].linePos, computeCharPos(lst[6].chrPos), lst[6].symbol, lst[6].chrPos));
                    ret.Add(new SyntaxToken(TokenKind.TK_NEWLINE, computeCharPos(lst[0].linePos), 0, "", lst[0].chrPos));
                    break;
            }

            return ret;
        }
    }
}