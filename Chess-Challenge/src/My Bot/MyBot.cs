﻿using ChessChallenge.API;
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

    double[] rSquareVals = {
        28, 32, 34, 34, 34, 34, 32, 28,
        32, 39, 43, 43, 43, 43, 39, 32,
        34, 43, 49, 49, 49, 49, 43, 34,
        34, 43, 49, 51, 51, 49, 43, 34,
        34, 43, 49, 51, 51, 49, 43, 34,
        34, 43, 49, 49, 49, 49, 43, 34,
        32, 39, 43, 43, 43, 43, 39, 32,
        28, 32, 34, 34, 34, 34, 32, 28
    };

    Dictionary<ulong, double> transpTable = new Dictionary<ulong, double>(); // transposition table: zobristkey -> score

    public Move Think(Board board, Timer timer)
    {
        Move[] moves = board.GetLegalMoves();

        // iterative depening?
        // assume 45 moves per game
        // this comes to 1333ms per move
        // if we take more than 45 moves i guess just give 1000ms per move
        int movesLeft = 45 - board.PlyCount / 2;
        int allowedTime = timer.MillisecondsRemaining / movesLeft;
        Console.WriteLine("predicting " + movesLeft + " moves left in game. allotted move time is " + allowedTime + "ms");

        Move moveToPlay = moves[new Random().Next(moves.Length)];
        double bestOutcome = double.NegativeInfinity;
        foreach(Move m in moves){
            board.MakeMove(m);
            double alphaBeta = AlphaBeta(board, 3, double.NegativeInfinity, double.PositiveInfinity, false, !board.IsWhiteToMove); // 3 seems to be the maximum reasonable search depth
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
            foreach(Piece p in pl) positioning += p.IsWhite ? rSquareVals[p.Square.Index] : -rSquareVals[p.Square.Index];
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

    // iterative version?
    double AlphaBeta(Board board, int depth, double a, double b, bool isMaximizing, bool perspective){
        ulong zobristKey = board.ZobristKey;
        //if(transpTable.ContainsKey(zobristKey)) return transpTable[zobristKey]; // hit in the transposition table!
        Move[] moves = board.GetLegalMoves();
        if(depth == 0 || moves.Length == 0){
            double score = Evaluate(board, perspective);
            //if(!transpTable.ContainsKey(zobristKey)) transpTable.Add(zobristKey, score); // exact value node (leaf node)
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
            //if(!transpTable.ContainsKey(zobristKey)) transpTable.Add(zobristKey, value); // inner node
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
        //if(!transpTable.ContainsKey(zobristKey)) transpTable.Add(zobristKey, value); // inner node
        return value;
    }
}