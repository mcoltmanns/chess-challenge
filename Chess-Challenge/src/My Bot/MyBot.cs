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

    Dictionary<ulong, (double, bool)> transpTable = new Dictionary<ulong, (double, bool)>(); // zobristkey -> best score, best move, depth found at, type (-1 lower bound, 0 exact, 1 upper bound)
    int capacity = 1000000; // 1 million entries allowed

    public Move Think(Board board, Timer timer)
    {
        Move[] moves = board.GetLegalMoves();

        // iterative depening?
        // assume 45 moves per game
        // this comes to 1333ms per move
        // if we take more than 45 moves i guess just give 1000ms per move
        int movesLeft = 45 - board.PlyCount / 2;
        int allowedTime = 500;//timer.MillisecondsRemaining / movesLeft;
        Console.WriteLine("predicting " + movesLeft + " moves left in game. allotted move time is " + allowedTime + "ms");

        transpTable.Clear();

        int depth = 0;
        Move moveToPlay = moves[new Random().Next(moves.Length)];
        while(timer.MillisecondsElapsedThisTurn < allowedTime){
            Console.WriteLine("searching to depth of " + depth);
            double bestOutcome = double.NegativeInfinity;
            foreach(Move m in moves){
                board.MakeMove(m);
                double alphaBeta = AlphaBeta(board, depth, double.NegativeInfinity, double.PositiveInfinity, false, !board.IsWhiteToMove); // 3 seems to be the maximum reasonable search depth
                if(alphaBeta > bestOutcome){
                    bestOutcome = alphaBeta;
                    moveToPlay = m;
                }
                board.UndoMove(m);
            }
            depth++;
        }

        // can turn ranks into numbers via char.ToUpper(*rank*) - 64
        // could be good for turning the rsquarevals into a function
        // seems logarithmic - outer files are much worse than inner files

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

        //----------HEURISTICS----------
        // bishop pair bonus (worth 1 additional bishop on top of the two)
        if(pieces[2].Count == 2) score += 350;
        if(pieces[8].Count == 2) score -= 350;

        //----------MOBILITY----------
        double mobility = board.GetLegalMoves().Length * 0.1;
        score += board.IsWhiteToMove ? mobility : -mobility;

        //----------POSITIONING----------
        double positioning = 0;
        foreach(PieceList pl in pieces){
            foreach(Piece p in pl) {
                Square s = p.Square;
                double gaussian = -Math.Exp(Math.Pow(s.Rank - 3.5, 2) / 11.23) - Math.Exp(Math.Pow(s.File - 3.5, 2) / 11.23) + 6; // use the addition of two gaussian curves to calculate the positional score ramps
                positioning += p.IsWhite ? gaussian : -gaussian;
            }
        }
        score += positioning * 0.5;
        
        //----------CHECKMATE----------
        if(board.IsInCheckmate()) score += board.IsWhiteToMove ? double.NegativeInfinity : double.PositiveInfinity;

        score = perspective ? score : -score; // apply negamax
        // anything after this line is buffs/debuffs regardless of side

        //----------REPEATS----------
        foreach(ulong key in board.GameRepetitionHistory) if(key == board.ZobristKey) score -= 1000000; // avoid repetitions

        return score; // positive good, negative bad!
    }

    double AlphaBeta(Board board, int depth, double a, double b, bool isMaximizing, bool perspective){
        if(transpTable.TryGetValue(board.ZobristKey, out (double, bool) info)){ // got a hit
            if(info.Item2) return info.Item1; // bounds don't need to be searched any deeper
        }

        Move[] moves = board.GetLegalMoves();
        double value;
        bool bound = false;

        if(depth == 0 || moves.Length == 0) {
            value = Evaluate(board, perspective);
        }
        else if(isMaximizing){
            value = double.NegativeInfinity;
            foreach(Move move in moves){
                board.MakeMove(move);
                value = Math.Max(value, AlphaBeta(board, depth - 1, a, b, false, perspective)); 
                board.UndoMove(move);
                if(value > b) {
                    bound = true;
                    break; // upper bound
                }
                a = Math.Max(a, value);
            }
        }
        else{
            value = double.PositiveInfinity;
            foreach(Move move in moves){
                board.MakeMove(move);
                value = Math.Min(value, AlphaBeta(board, depth - 1, a, b, true, perspective));
                board.UndoMove(move);
                if(value < a) {
                    bound = true;
                    break; // lower bound
                }
                b = Math.Min(b, value);
            }
        }

        if(!transpTable.TryAdd(board.ZobristKey, (value, bound))) transpTable[board.ZobristKey] = (value, bound); // always overwrite existing keys, since the pruning is taken care of in AlphaBeta()

        return value;
    }
}