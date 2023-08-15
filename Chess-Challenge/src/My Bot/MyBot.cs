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

    // table sources: https://i.stack.imgur.com/hxGdi.png
    double[] knightValues = {
        -5, -4, -3, -3, -3, -3, -4, -5,
        -4, -2, 0, 0, 0, 0, -2, -4,
        -3, 0, 1, 1.5, 1.5, 1, 0, -3,
        -3, 0.5, 1.5, 2, 2, 1.5, 0.5, -3,
        -3, 0.5, 1.5, 2, 2, 1.5, 0.5, -3,
        -3, 0, 1, 1.5, 1.5, 1, 0, -3,
        -4, -2, 0, 0, 0, 0, -2, -4,
        -5, -4, -3, -3, -3, -3, -4, -5
    };

    double[] pawnValues = {
        0, 0, 0, 0, 0, 0, 0, 0,
        5, 5, 5, 5, 5, 5, 5, 5,
        1, 1, 2, 3, 3, 2, 1, 1,
        0.5, 0.5, 1, 2.5, 2.5, 1, 0.5, 0.5,
        0, 0, 0, 2, 2, 0, 0, 0,
        0.5, -0.5, -1, 0, 0, -1, -0.5, 0.5,
        0.5, 1, 1, -2, -2, 1, 1, 0.5,
        0, 0, 0, 0, 0, 0, 0, 0
    };

    double[] bishopValues = {
        -2, -1, -1, -1, -1, -1, -1, -2,
        -1, 0, 0, 0, 0, 0, 0, -1,
        -1, 0, 0.5, 1, 1, 0.5, 0, -1,
        -1, 0.5, 0.5, 1, 1, 0.5, 0.5, -1,
        -1, 0, 1, 1, 1, 1, 0, -1,
        -1, 1, 1, 1, 1, 1, 1, -1,
        -1, 0.5, 0, 0, 0, 0, 0.5, -1,
        -2, -1, -1, -1, -1, -1, -1, -2
    };

    public Move Think(Board board, Timer timer)
    {
        Move[] allMoves = board.GetLegalMoves();

        Move moveToPlay = allMoves[new Random().Next(allMoves.Length)];
        double bestOutcome = double.NegativeInfinity;
        foreach(Move m in allMoves){
            board.MakeMove(m);
            double alphaBeta = AlphaBeta(board, 3, double.NegativeInfinity, double.PositiveInfinity, false, !board.IsWhiteToMove); // 3 seems to be the maximum reasonable search depth
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
    double Evaluate(Board board, bool perspective){
        double score = 0;
        // all evaluations in here are done from white's side, with positive good/negative bad. then depending on perspective, multiply by -1
        // piece lists
        PieceList[] pieces = board.GetAllPieceLists(); // wp(0), wkn(1), wb(2), wr(3), wq(4), wk(5), bp(6), bkn(7), bb(8), br(9), bq(10), bk(11)

        //-----------MATERIAL----------
        double material = pieceValueLookup[PieceType.King] * (pieces[5].Count - pieces[11].Count)
            + pieceValueLookup[PieceType.Queen] * (pieces[4].Count - pieces[10].Count)
            + pieceValueLookup[PieceType.Rook] * (pieces[3].Count - pieces[9].Count)
            + pieceValueLookup[PieceType.Knight] * (pieces[2].Count - pieces[8].Count)
            + pieceValueLookup[PieceType.Bishop] * (pieces[1].Count - pieces[7].Count)
            + pieceValueLookup[PieceType.Pawn] * (pieces[0].Count - pieces[6].Count);
        score += material * 1; // weight all material

        //----------HEURISTICS----------
        // castle bonus - avoid states in which we haven't castled! (2 pawns worth)
        if(board.HasKingsideCastleRight(true) || board.HasQueensideCastleRight(true)) score -= 200; // avoid states in which we still have the right to castle
        if(board.HasKingsideCastleRight(false) || board.HasQueensideCastleRight(false)) score += 200; // prefer states in which the opponent still has the right to castle

        // bishop pair bonus (worth 1 additional bishop on top of the two)
        if(pieces[2].Count == 2) score += 350;
        if(pieces[8].Count == 2) score -= 350;

        //----------MOBILITY----------
        // figure out which squares each side can move to
        ulong whiteMovesBb = 0;
        ulong blackMovesBb = 0;
        double pSqVals = 0;
        for(int i = 0; i < 6; i++){ // piece by piece processing done in this block
            PieceList white = pieces[i];
            PieceList black = pieces[i + 6];
            (ulong, double) info = EvaluateMobilityAndPieceSquareVals(board, white, true);
            whiteMovesBb |= info.Item1;
            pSqVals += info.Item2;
            info = EvaluateMobilityAndPieceSquareVals(board, black, false);
            blackMovesBb |= info.Item1;
            pSqVals -= info.Item2;
        }

        //----------WEIGHTED SUM----------
        score += (BitboardHelper.GetNumberOfSetBits(whiteMovesBb) - BitboardHelper.GetNumberOfSetBits(blackMovesBb)) * 0.2 // raw mobility
                + pSqVals * 10 // piece square values
                + material; // material
        
        //----------CHECKMATE----------
        if(board.IsInCheckmate()) score += board.IsWhiteToMove ? double.NegativeInfinity : double.PositiveInfinity;

        return perspective ? score : -score; // positive good, negative bad!
    }

    // iterative version?
    double AlphaBeta(Board board, int depth, double a, double b, bool isMaximizing, bool perspective){
        Move[] moves = board.GetLegalMoves();
        if(depth == 0 || moves.Length == 0){
            return Evaluate(board, perspective);
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
            return value;
        }
        else{
            value = double.PositiveInfinity;
            foreach(Move move in moves){
                board.MakeMove(move);
                value = Math.Min(value, AlphaBeta(board, depth - 1, a, b, true, perspective));
                board.UndoMove(move);
                if(value < a) break;
                b = Math.Min(b, value);
            }
            return value;
        }
    }

    // ulong is bitboard of squares reachable by all pieces in piecelist
    // double is total piece square value
    (ulong, double) EvaluateMobilityAndPieceSquareVals(Board board, PieceList pieces, bool perspective){
        ulong movesBb = 0;
        double pSqVals = 0;
        for(int j = 0; j < pieces.Count; j++){
            Piece p = pieces[j];
            movesBb |= BitboardHelper.GetPieceAttacks(p.PieceType, p.Square, board, perspective); // raw mobility
            switch(p.PieceType){
                case PieceType.Knight: // knight piece square
                    pSqVals += knightValues[p.Square.Index];
                    break;
                case PieceType.Pawn:
                    pSqVals += pawnValues[p.Square.Index];
                    break;
                case PieceType.Bishop:
                    pSqVals += bishopValues[p.Square.Index];
                    break;
            }
        }
        return (movesBb, pSqVals);
    }
}