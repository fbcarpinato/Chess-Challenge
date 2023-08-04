using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ChessChallenge.Example
{
    public class MyBot : IChessBot
    {
        int maxDepth = 4;

        int[] pieceValues = { 0, 100, 320, 330, 500, 900, 20000 };

        readonly int capacity = 10000000;
        readonly LinkedList<ulong> lruList = new LinkedList<ulong>();
        readonly Dictionary<ulong, Tuple<int, Move, int>> transpositionTable = new Dictionary<ulong, Tuple<int, Move, int>>();

        public Move Think(Board board, Timer timer)
        {
            Move bestMove = new Move();

            for (int depth = 1; depth <= maxDepth; depth++)
            {
                var result = Minimax(board, depth, true, int.MinValue, int.MaxValue, bestMove);
                bestMove = result.Item2;
            }

            return bestMove;
        }

        public Tuple<int, Move> Minimax(Board board, int depth, bool maximizingPlayer, int alpha, int beta, Move lastBestMove)
        {
            if (transpositionTable.TryGetValue(board.ZobristKey, out Tuple<int, Move, int> cachedValue) && cachedValue.Item3 >= depth)
            {
                return new Tuple<int, Move>(cachedValue.Item1, cachedValue.Item2);
            }

            if (depth == 0 || board.IsDraw() || board.IsInCheckmate())
            {
                return new Tuple<int, Move>(EvaluateBoard(board), new Move());
            }

            Move bestMove = lastBestMove;
            var moves = board.GetLegalMoves();
            moves = OrderMoves(board, moves, bestMove);

            if (maximizingPlayer)
            {
                int maxEval = int.MinValue;
                foreach (Move move in moves)
                {
                    board.MakeMove(move);
                    int eval = Minimax(board, depth - 1, false, alpha, beta, bestMove).Item1;
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
                AddToTranspositionTable(board.ZobristKey, new Tuple<int, Move, int>(maxEval, bestMove, depth));
                return new Tuple<int, Move>(maxEval, bestMove);
            }
            else
            {
                int minEval = int.MaxValue;
                foreach (Move move in moves)
                {
                    board.MakeMove(move);
                    int eval = Minimax(board, depth - 1, true, alpha, beta, bestMove).Item1;
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
                AddToTranspositionTable(board.ZobristKey, new Tuple<int, Move, int>(minEval, bestMove, depth));
                return new Tuple<int, Move>(minEval, bestMove);
            }
        }

        public int EvaluateBoard(Board board)
        {
            if (board.IsInCheckmate())
            {
                return int.MaxValue;
            }

            int[] material = new int[2];
            int[] mobility = new int[2];
            int[] checks = new int[2];

            for (int pieceType = 0; pieceType <= 6; pieceType++)
            {
                for (int color = 0; color <= 1; color++)
                {
                    ulong pieces = board.GetPieceBitboard((PieceType)pieceType, color == 0);

                    for (int sq = 0; sq < 64; sq++)
                    {
                        if (((pieces >> sq) & 1) == 1)
                        {
                            material[color] += pieceValues[pieceType];
                        }
                    }
                }
            }


            foreach (Move move in board.GetLegalMoves())
            {
                mobility[board.IsWhiteToMove ? 0 : 1]++;

                board.MakeMove(move);
                if (board.IsInCheck()) checks[board.IsWhiteToMove ? 0 : 1]++;
                board.UndoMove(move);
            }

            board.MakeMove(new Move());
            foreach (Move move in board.GetLegalMoves())
            {
                mobility[board.IsWhiteToMove ? 0 : 1]++;

                board.MakeMove(move);
                if (board.IsInCheck()) checks[board.IsWhiteToMove ? 0 : 1]++;
                board.UndoMove(move);
            }
            board.UndoMove(new Move());

            int firstIndex = board.IsWhiteToMove ? 0 : 1;
            int secondIndex = board.IsWhiteToMove ? 1 : 0;

            int materialMgScore = material[firstIndex] - material[secondIndex];
            int mobilityMgScore = mobility[firstIndex] - mobility[secondIndex];
            int checksMgScore = checks[firstIndex] * 10 - checks[secondIndex] * 10;

            int mgScore = materialMgScore
                + mobilityMgScore
                + checksMgScore;

            return mgScore;
        }

        Move[] OrderMoves(Board board, Move[] moves, Move lastBestMove)
        {
            var scoredMoves = moves.Select(move => new
            {
                Move = move,
                Score = GetMoveScore(board, move)
            });

            var orderedMoves = scoredMoves.OrderByDescending(x => x.Score).Select(x => x.Move);

            return orderedMoves.ToArray();
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

            // Encourage piece development and king safety.
            if (capturingPiece.PieceType == PieceType.Knight || capturingPiece.PieceType == PieceType.Bishop)
                score += 50;

            // Define the center of the board.
            var centerFiles = new[] { 3, 4 };
            var centerRanks = new[] { 3, 4 };

            if (centerFiles.Contains(move.TargetSquare.File) && centerRanks.Contains(move.TargetSquare.Rank))
                score += 10;

            if (move.IsCastles)
                score += 100;

            board.UndoMove(move);

            return score;
        }

        private void AddToTranspositionTable(ulong key, Tuple<int, Move, int> value)
        {
            if (transpositionTable.Count >= capacity)
            {
                RemoveFirst();
            }

            transpositionTable[key] = value;
            lruList.AddLast(key);
        }

        private void RemoveFirst()
        {
            ulong key = lruList.First.Value;
            lruList.RemoveFirst();
            transpositionTable.Remove(key);
        }

    }
}
