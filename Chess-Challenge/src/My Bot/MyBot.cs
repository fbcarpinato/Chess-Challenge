﻿using ChessChallenge.API;
using System;
using System.Linq;

namespace ChessChallenge.Example
{
    public class MyBot : IChessBot
    {
        int maxDepth = 6;

        int[] pieceValues = { 0, 100, 320, 330, 500, 900, 20000 };

        public Move Think(Board board, Timer timer)
        {
            return Minimax(board, maxDepth, true, int.MinValue, int.MaxValue).Item2;
        }

        public Tuple<int, Move> Minimax(Board board, int depth, bool maximizingPlayer, int alpha, int beta)
        {
            if (depth == 0 || board.IsDraw() || board.IsInCheckmate())
            {
                return new Tuple<int, Move>(EvaluateBoard(board), new Move());
            }

            Move bestMove = new Move();
            var moves = board.GetLegalMoves();
            moves = OrderMoves(board, moves);

            if (maximizingPlayer)
            {
                int maxEval = int.MinValue;
                foreach (Move move in board.GetLegalMoves())
                {
                    board.MakeMove(move);
                    int eval = Minimax(board, depth - 1, false, alpha, beta).Item1;
                    board.UndoMove(move);
                    if (eval > maxEval)
                    {
                        maxEval = eval;
                        bestMove = move;
                    }
                    alpha = Math.Max(alpha, eval);
                    if (beta <= alpha)
                        break;
                }
                return new Tuple<int, Move>(maxEval, bestMove);
            }
            else
            {
                int minEval = int.MaxValue;
                foreach (Move move in moves)
                {
                    board.MakeMove(move);
                    int eval = Minimax(board, depth - 1, true, alpha, beta).Item1;
                    board.UndoMove(move);
                    if (eval < minEval)
                    {
                        minEval = eval;
                        bestMove = move;
                    }
                    beta = Math.Min(beta, eval);
                    if (beta <= alpha)
                        break;
                }
                return new Tuple<int, Move>(minEval, bestMove);
            }
        }

        public int EvaluateBoard(Board board)
        {
            if (board.IsInCheckmate())
            {
                return int.MaxValue;
            }

            int gamePhase = 0;
            int[] material = new int[2];
            int[] mobility = new int[2];
            int[] checks = new int[2];

            for (int pieceType = 0; pieceType <= 5; pieceType++)
            {
                for (int color = 0; color <= 1; color++)
                {
                    ulong pieces = board.GetPieceBitboard((PieceType)pieceType, color == 0);

                    for (int sq = 0; sq < 64; sq++)
                    {
                        if (((pieces >> sq) & 1) == 1)
                        {
                            gamePhase += pieceValues[pieceType];
                            material[color] += pieceValues[pieceType];
                        }
                    }
                }
            }

            foreach (Move move in board.GetLegalMoves())
            {
                board.MakeMove(move);
                mobility[board.IsWhiteToMove ? 0 : 1]++;
                if (board.IsInCheck()) checks[board.IsWhiteToMove ? 0 : 1]++;
                board.UndoMove(move);
            }

            int mgScore = material[board.IsWhiteToMove ? 0 : 1] - material[board.IsWhiteToMove ? 1 : 0] + mobility[board.IsWhiteToMove ? 0 : 1] - mobility[board.IsWhiteToMove ? 1 : 0] + checks[board.IsWhiteToMove ? 0 : 1] * 10 - checks[board.IsWhiteToMove ? 1 : 0] * 10;
            return mgScore;
        }

        Move[] OrderMoves(Board board, Move[] moves)
        {
            var scoredMoves = moves.Select(move => new
            {
                Move = move,
                Score = GetMoveScore(board, move)
            });

            return scoredMoves.OrderByDescending(x => x.Score).Select(x => x.Move).ToArray();
        }

        int GetMoveScore(Board board, Move move)
        {
            int score = 0;

            Piece capturingPiece = board.GetPiece(move.StartSquare);
            Piece capturedPiece = board.GetPiece(move.TargetSquare);

            score += pieceValues[(int)capturedPiece.PieceType] - pieceValues[(int)capturingPiece.PieceType];

            if (move.IsPromotion)
                score += pieceValues[(int)move.PromotionPieceType] - pieceValues[(int)PieceType.Pawn];

            board.MakeMove(move);
            if (board.IsInCheckmate())
                score += 1000000;
            else if (board.IsInCheck())
                score += 5000;
            board.UndoMove(move);

            return score;
        }

    }
}