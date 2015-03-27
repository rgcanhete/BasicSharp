﻿using BasicSharp.Compiler.Lexer;
using BasicSharp.Compiler.Parser.Syntaxes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BasicSharp.Compiler.Parser.Extensions;
using lxr = BasicSharp.Compiler.Lexer;

namespace BasicSharp.Compiler.Parser
{
    public class Parser
    {
        readonly lxr.Lexer lexer;
        readonly IEnumerator<TokenInfo> enumerator;
        readonly Queue<TokenInfo> triviaBuffer;

        public Parser(lxr.Lexer lexer)
        {
            this.lexer = lexer;
            this.enumerator = lexer.GetTokens().GetEnumerator();
            this.triviaBuffer = new Queue<TokenInfo>();
        }

        public SyntaxNode GetSyntax()
        {
            enumerator.MoveNext();
            SyntaxNode root;

            if ((root = getCompilationUnit()) != null)
                return root;

            if ((root = getFieldOrMethodDeclaration()) != null)
                return root;

            throw new NotImplementedException();
        }

        #region CompilerUnit
        CompilationUnit getCompilationUnit()
        {
            var hasImplementsDirective = false;
            var result = new CompilationUnit();

            ImplementsDirective currImplDir;
            while ((currImplDir = getImplementsDirective()) != null)
            {
                hasImplementsDirective = true;
                result.AddImplementsDirective(currImplDir);
                dumpTriviaBufferInto(result);
            }

            if (!hasImplementsDirective)
                return null;

            var module = getModuleDeclaration();
            dumpTriviaBufferInto(result);

            if (module == null)
                throw null;

            result.Module = module;

            return result;
        }

        ImplementsDirective getImplementsDirective()
        {
            var implements = currentToken();

            if (implements.Kind != SyntaxKind.ImplementsDirectiveKeyword)
                return null;

            var result = new ImplementsDirective { ImplementsToken = implements };

            while (consumeCurrentTokenAndGetNext().Kind.IsIn(SyntaxKind.Identifier, SyntaxKind.DotToken))
                result.AddFullClassNamePart(currentToken());

            if (currentToken().Kind != SyntaxKind.SemicolonToken)
                throw null;

            result.Semicolon = currentToken();
            consumeCurrentTokenAndGetNext();
            dumpTriviaBufferInto(result);

            return result;
        }
        ModuleDeclaration getModuleDeclaration()
        {
            var module = currentToken();
            if (module.Kind != SyntaxKind.ModuleKeyword)
                return null;

            var result = new ModuleDeclaration { ModuleToken = module };

            var name = consumeCurrentTokenAndGetNext();
            if (name.Kind != SyntaxKind.Identifier)
                throw null;

            dumpTriviaBufferInto(result);
            result.Name = name;

            dumpTriviaBufferInto(result);

            var openBrace = consumeCurrentTokenAndGetNext();
            if (openBrace.Kind != SyntaxKind.OpenBraceToken)
                throw null;

            result.OpenBrace = openBrace;
            consumeCurrentTokenAndGetNext();

            dumpTriviaBufferInto(result);

            ModuleMemberDeclaration member;
            while ((member = getFieldOrMethodDeclaration()) != null)
            {
                result.AddMember(member);
                dumpTriviaBufferInto(result);
            }

            var closeBrace = consumeCurrentTokenAndGetNext();
            if (closeBrace.Kind != SyntaxKind.CloseBraceToken)
                throw null;

            result.CloseBrace = closeBrace;
            consumeCurrentTokenAndGetNext();

            return result;
        }

        #endregion

        #region FieldOrMethodDeclaration
        ModuleMemberDeclaration getFieldOrMethodDeclaration()
        {
            var modifier = currentToken();
            if (!modifier.Kind.IsModifier())
                return null;

            consumeCurrentTokenAndGetNext();
            var type = getPredefinedType();
            if (type == null)
                throw null; //Type esperado, etc etc

            var identifier = currentToken();
            if (identifier.Kind != SyntaxKind.Identifier)
                throw null; //Type esperado, etc etc

            consumeCurrentTokenAndGetNext();
            var parameterList = getParameterList();
            if (parameterList != null)
                return new MethodDeclaration
                {
                    Modifier = modifier,
                    ReturnType = type,
                    Identifier = identifier,
                    ParameterList = parameterList,
                    Block = getBlock()
                };

            return getFieldDeclaration(modifier, type, identifier);
        }
        PredefinedType getPredefinedType()
        {
            var type = currentToken();

            if (!type.Kind.IsTypeNotContextual())
                return null;

            var result = new PredefinedType { TypeToken = type };

            var openBracket = consumeCurrentTokenAndGetNext();
            if (openBracket.Kind == SyntaxKind.OpenBracketToken)
            {
                consumeCurrentTokenAndGetNext();

                var expression = getExpression();
                var closeBracket = currentToken();
                if (closeBracket.Kind != SyntaxKind.CloseBracketToken)
                    throw null;

                result.ArraySpecifier = new ArrayRankSpecifier
                {
                    OpenBracket = openBracket,
                    Value = expression,
                    CloseBracket = closeBracket
                };

                consumeCurrentTokenAndGetNext();
            }

            return result;
        }

        FieldDeclaration getFieldDeclaration(TokenInfo modifier, PredefinedType type, TokenInfo identifier)
        {
            var result = new FieldDeclaration { Modifier = modifier };

            var variableDeclaration = VariableDeclaration.WithDeclarator(getVariableDeclarator<ConstantAssignmentExpression>(identifier));
            variableDeclaration.Type = type;
            result.Declaration = variableDeclaration;

            var current = currentToken();
            if (current.Kind == SyntaxKind.SemicolonToken)
            {
                dumpTriviaBufferInto(variableDeclaration);
                consumeCurrentTokenAndGetNext();
                result.Semicolon = current;
                return result;
            }

            dumpTriviaBufferInto(variableDeclaration);
            if (current.Kind != SyntaxKind.CommaToken)
                throw null;

            variableDeclaration.AddTrivia(currentToken());
            consumeCurrentTokenAndGetNext();
            VariableDeclarator<ConstantAssignmentExpression> varDecl;
            while ((varDecl = getVariableDeclarator<ConstantAssignmentExpression>()) != null)
            {
                dumpTriviaBufferInto(variableDeclaration);
                variableDeclaration.AddDeclarator(varDecl);

                if (currentToken().Kind == SyntaxKind.CommaToken)
                {
                    variableDeclaration.AddTrivia(currentToken());
                    consumeCurrentTokenAndGetNext();
                }
            }

            if (currentToken().Kind != SyntaxKind.SemicolonToken)
                throw null;

            result.Semicolon = currentToken();

            consumeCurrentTokenAndGetNext();
            return result;
        }
        VariableDeclarator<T> getVariableDeclarator<T>()
            where T : AssignmentExpression
        {
            var current = currentToken();

            if (current.Kind != SyntaxKind.Identifier)
                return null;

            consumeCurrentTokenAndGetNext();
            return getVariableDeclarator<T>(current);
        }

        VariableDeclarator<T> getVariableDeclarator<T>(TokenInfo identifier)
            where T : AssignmentExpression
        {
            var current = currentToken();

            if (current.Kind.IsIn(SyntaxKind.CommaToken, SyntaxKind.SemicolonToken))
                return new VariableDeclarator<T> { Identifier = identifier };

            if (current.Kind == SyntaxKind.EqualsToken)
                return new VariableDeclarator<T> { Identifier = identifier, Assignment = getAssignmentExpression<T>(current) };

            throw null;
        }
        T getAssignmentExpression<T>(TokenInfo assignmentOperatorInfo)
            where T : AssignmentExpression
        {
            if (typeof(T) == typeof(ConstantAssignmentExpression))
                throw null;

            if (typeof(T) == typeof(AssignmentExpression))
                throw null;

            throw null;
        }

        ParameterList getParameterList()
        {
            var openParen = currentToken();
            if (openParen.Kind != SyntaxKind.OpenParenToken)
                return null;

            var result = new ParameterList { OpenParenToken = openParen };
            consumeCurrentTokenAndGetNext();

            Parameter currParam;
            while ((currParam = getParameter()) != null)
            {
                dumpTriviaBufferInto(result);
                result.AddParameter(currParam);

                var comma = currentToken();
                if (comma.Kind == SyntaxKind.CommaToken)
                {
                    dumpTriviaBufferInto(result);
                    result.AddTrivia(comma);
                    consumeCurrentTokenAndGetNext();
                }
            }

            var closeParen = currentToken();
            if (closeParen.Kind != SyntaxKind.CloseParenToken)
                throw null;

            dumpTriviaBufferInto(result);
            result.CloseParenToken = closeParen;

            consumeCurrentTokenAndGetNext();
            return result;
        }
        Parameter getParameter()
        {
            var type = currentToken();
            if (!type.Kind.IsTypeNotContextual())
                return null;

            var identifier = consumeCurrentTokenAndGetNext();
            if (identifier.Kind != SyntaxKind.Identifier)
                throw null;

            consumeCurrentTokenAndGetNext();
            var result = new Parameter { Type = type, Identifier = identifier };
            dumpTriviaBufferInto(result);

            return result;
        }
        //MethodDeclaration ::=  Modifier? Method_Type Identifier Parameters_List Block
        MethodDeclaration getMethod()
        {
            throw new NotImplementedException();
        }
        #endregion

        #region Block members
        Block getBlock()
        {
            var openBrace = currentToken();
            if (openBrace.Kind != SyntaxKind.OpenBraceToken)
                return null;

            TokenInfo closeBrace;
            while ((closeBrace = consumeCurrentTokenAndGetNext()).Kind != SyntaxKind.CloseBraceToken) { }

            consumeCurrentTokenAndGetNext();
            return new Block { OpenBrace = openBrace, CloseBrace = closeBrace };
        }

        LocalVariableDeclaration getLocalVariableDeclaration()
        {
            var type = getPredefinedType();
            if (type == null)
                return null;
            return null;


        }

        #endregion

        //TODO: Continue from here, think about LiteralExpression
        #region Expressions
        Expression getExpression()
        {
            return new ConstantAssignmentExpression();
        }

        ParenthesedExpression getParenthesedExpression()
        {
            var openParen = currentToken();
            if (openParen.Kind != SyntaxKind.OpenParenToken)
                return null;

            consumeCurrentTokenAndGetNext();
            var innerExpression = getExpression();

            if (innerExpression == null)
                throw null;

            var closeParen = currentToken();
            if (closeParen.Kind != SyntaxKind.CloseParenToken)
                throw null;

            consumeCurrentTokenAndGetNext();
            return new ParenthesedExpression
            {
                OpenParenToken = openParen,
                InnerExpression = innerExpression,
                CloseParenToken = closeParen
            };
        }

        Expression getInvocationOrAccessorExpression()
        {
            var identifier = currentToken();
            if (identifier.Kind != SyntaxKind.Identifier)
                return null;

            var openParen = consumeCurrentTokenAndGetNext();
            if (openParen.Kind != SyntaxKind.OpenParenToken)
                return new AccessorExpression { Identifier = identifier };

            var result = new InvocationExpression { MethodName = identifier };
            dumpTriviaBufferInto(result);

            var arguments = new ArgumentList { OpenParen = openParen };
            result.Arguments = arguments;

            TokenInfo c;
            while ((c = consumeCurrentTokenAndGetNext()).Kind != SyntaxKind.CloseParenToken)
            {
                dumpTriviaBufferInto(arguments);

                var expr = getExpression();
                if (expr != null)
                {
                    arguments.AddArgument(new Argument { Expression = expr });
                    if (currentToken().Kind == SyntaxKind.CommaToken)
                    {
                        arguments.AddTrivia(currentToken());
                        consumeCurrentTokenAndGetNext();
                    }
                }
            }

            arguments.CloseParen = currentToken();
            dumpTriviaBufferInto(arguments);

            return result;
        }

        Expression getAdditiveExpression()
        {
            return getUnaryOrBinary(getMultiplicativeExpression, SyntaxKind.PlusToken, SyntaxKind.MinusToken);
        }

        Expression getMultiplicativeExpression()
        {
            return getUnaryOrBinary(getModExpression, SyntaxKind.AsteriskToken, SyntaxKind.SlashToken);
        }

        Expression getModExpression()
        {
            return getUnaryOrBinary(getUnaryExpression, SyntaxKind.ModToken);
        }

        Expression getUnaryExpression()
        {
            var signal = currentToken();
            if (!signal.Kind.IsIn(SyntaxKind.MinusToken, SyntaxKind.PlusToken))
                return getExpression();

            return new UnaryExpression { SignalToken = signal, Expression = getExpression() };
        }

        Expression getUnaryOrBinary(Func<Expression> precedence, params SyntaxKind[] operators)
        {
            var leftSide = getExpression();
            if (leftSide == null)
                return null;

            dumpTriviaBufferInto(leftSide);

            var _operator = consumeCurrentTokenAndGetNext();
            if (!_operator.Kind.IsIn(operators))
                return leftSide;

            var rightSide = precedence();
            if (rightSide == null)
                throw null;

            dumpTriviaBufferInto(rightSide);

            return new BinaryExpression
            {
                LeftSide = leftSide,
                OperatorToken = _operator,
                RightSide = rightSide
            };
        }
        #endregion

        void dumpTriviaBufferInto(SyntaxNode node)
        {
            while (triviaBuffer.Count > 0)
                node.AddTrivia(triviaBuffer.Dequeue());
        }
        TokenInfo currentToken()
        {
            if (enumerator.Current.Kind.IsTrivia())
                return consumeCurrentTokenAndGetNext();

            return enumerator.Current;
        }
        TokenInfo consumeCurrentTokenAndGetNext()
        {
            while (enumerator.MoveNext())
            {
                if (enumerator.Current.Kind.IsTrivia())
                    triviaBuffer.Enqueue(enumerator.Current);
                else
                    break;
            }

            return enumerator.Current;
        }

    }
}