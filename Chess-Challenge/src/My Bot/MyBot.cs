using ChessChallenge.API;
using System.Collections.Generic;
using System;

public class MyBot : IChessBot
{
    Dictionary<PieceType, double> pieceValueLookup = new Dictionary<PieceType, double>() // lookups for piece values
    {
        {PieceType.None, 0},
        {PieceType.Pawn, 100},
        {PieceType.Knight, 325},
        {PieceType.Bishop, 350},
        {PieceType.Rook, 525},
        {PieceType.Queen, 1000},
        {PieceType.King, 20000}
    };

    Dictionary<ulong, (double, int, int)> transpTable = new Dictionary<ulong, (double, int, int)>(); // transposition table: zobristkey -> score, depth, node type (0: true value - 1: upper bound - 2: lower bound)

    public Move Think(Board board, Timer timer)
    {
        System.Span<Move> moves = stackalloc Move[40];
        board.GetLegalMovesNonAlloc(ref moves);

        Move moveToPlay = moves[new Random().Next(moves.Length)];
        double bestOutcome = double.NegativeInfinity;
        foreach(Move m in moves){
            board.MakeMove(m);
            double alphaBeta = AlphaBeta(board, 5, double.NegativeInfinity, double.PositiveInfinity, false, !board.IsWhiteToMove); // 3 seems to be the maximum reasonable search depth
            // focus on either improving the eval, or speeding up search somehow to get more depth out of it
            if(alphaBeta > bestOutcome){
                bestOutcome = alphaBeta;
                moveToPlay = m;
            }
            board.UndoMove(m);
        }

        return moveToPlay;
    }

    // evaluate with negamax - scored relative to side specified
    double Evaluate(Board board, bool perspective){// all evaluations in here are done from white's side, with positive good/negative bad. then depending on perspective, multiply by -1
        // piece lists
        PieceList[] pieces = board.GetAllPieceLists(); // wp(0), wkn(1), wb(2), wr(3), wq(4), wk(5), bp(6), bkn(7), bb(8), br(9), bq(10), bk(11)

        //-----------MATERIAL----------
        double score = pieceValueLookup[PieceType.King] * (pieces[5].Count - pieces[11].Count)
            + pieceValueLookup[PieceType.Queen] * (pieces[4].Count - pieces[10].Count)
            + pieceValueLookup[PieceType.Rook] * (pieces[3].Count - pieces[9].Count)
            + pieceValueLookup[PieceType.Knight] * (pieces[2].Count - pieces[8].Count)
            + pieceValueLookup[PieceType.Bishop] * (pieces[1].Count - pieces[7].Count)
            + pieceValueLookup[PieceType.Pawn] * (pieces[0].Count - pieces[6].Count);
        
        //----------CHECKMATE----------
        if(board.IsInCheckmate()) score += board.IsWhiteToMove ? double.NegativeInfinity : double.PositiveInfinity;

        return perspective ? score : -score; // positive good, negative bad!
    }

    // iterative version?
    double AlphaBeta(Board board, int depth, double a, double b, bool isMaximizing, bool perspective){
        ulong zobristKey = board.ZobristKey;
        if(transpTable.ContainsKey(zobristKey)) return transpTable[zobristKey].Item1; // hit in the transposition table! decide if it's worth it to keep searching?
        Move[] moves = board.GetLegalMoves();
        if(depth == 0 || moves.Length == 0){
            double score = Evaluate(board, perspective);
            transpTable.Add(zobristKey, (score, depth, 0)); // exact value node (leaf node)
            return score;
        }

        double value;
        if(isMaximizing){
            value = double.NegativeInfinity;
            foreach(Move move in moves){
                board.MakeMove(move);
                value = Math.Max(value, AlphaBeta(board, depth - 1, a, b, false, perspective)); 
                board.UndoMove(move);
                if(value > b) break;
                a = Math.Max(a, value);
            }
            if(!transpTable.ContainsKey(zobristKey)) transpTable.Add(zobristKey, (value, depth, 1)); // inner node
            return value;
        }

        value = double.PositiveInfinity;
        foreach(Move move in moves){
            board.MakeMove(move);
            value = Math.Min(value, AlphaBeta(board, depth - 1, a, b, true, perspective));
            board.UndoMove(move);
            if(value < a) break;
            b = Math.Min(b, value);
        }
        if(!transpTable.ContainsKey(zobristKey)) transpTable.Add(zobristKey, (value, depth, 1)); // inner node
        return value;
    }
}