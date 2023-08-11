using ChessChallenge.API;
using System.Collections.Generic;
using System;

public class MyBot : IChessBot
{
    static Dictionary<PieceType, float> pieceValueLookup = new Dictionary<PieceType, float>() // lookups for piece values
    {
        {PieceType.None, 0f},
        {PieceType.Pawn, 1f},
        {PieceType.Knight, 3f},
        {PieceType.Bishop, 3f},
        {PieceType.Rook, 5f},
        {PieceType.Queen, 9f},
        {PieceType.King, 200f}
    };

    static Dictionary<ulong, float> stateInfo = new Dictionary<ulong, float>();

    int searchDepth = 2;

    public Move Think(Board board, Timer timer)
    {
        Console.WriteLine("searching to a depth of " + searchDepth + "...");
        Move[] allMoves = board.GetLegalMoves();
        bool color = board.IsWhiteToMove;

        // Pick a random move to play if nothing better is found
        Random rng = new();
        Move moveToPlay = allMoves[rng.Next(allMoves.Length)];

        float bestOutcome = float.NegativeInfinity;
        foreach(Move m in allMoves){
            board.MakeMove(m);
            float alphaBeta = AlphaBeta(board, searchDepth, float.NegativeInfinity, float.PositiveInfinity, false, color);
            if(alphaBeta > bestOutcome){
                bestOutcome = alphaBeta;
                moveToPlay = m;
            }
            board.UndoMove(m);
        }

        // deep searches are less important with fewer pieces on the board
        // usually there are 40 moves per game, so should take about 1.5 sec per move
        if(timer.MillisecondsElapsedThisTurn < 1250) searchDepth ++; // if we were fast, go deeper next round - constant is arbitrary, tweak as needed
        else searchDepth --; // otherwise go shallower

        // i think we need some more heuristics

        Console.WriteLine("found best move " + bestOutcome + " in " + timer.MillisecondsElapsedThisTurn + "ms");

        return moveToPlay;
    }

    // evaluate with negamax - scored relative to side specified
    float Evaluate(Board board, bool perspective){
        float score = 0f;
        // all evaluations in here are done from white's side, with positive good/negative bad. then depending on perspective, multiply by -1
        // piece lists
        PieceList[] pieces = board.GetAllPieceLists(); // wp(0), wkn(1), wb(2), wr(3), wq(4), wk(5), bp(6), bkn(7), bb(8), br(9), bq(10), bk(11)

        // consider material
        float material = pieceValueLookup[PieceType.King] * (pieces[5].Count - pieces[11].Count)
            + pieceValueLookup[PieceType.Queen] * (pieces[4].Count - pieces[10].Count)
            + pieceValueLookup[PieceType.Rook] * (pieces[3].Count - pieces[9].Count)
            + pieceValueLookup[PieceType.Knight] * (pieces[2].Count - pieces[8].Count)
            + pieceValueLookup[PieceType.Bishop] * (pieces[1].Count - pieces[7].Count)
            + pieceValueLookup[PieceType.Pawn] * (pieces[0].Count - pieces[6].Count);
        score += material * 1f; // weight all material

        // consider mobility
        // figure out which squares each side can move to
        ulong whiteMovesBb = 0;
        ulong blackMovesBb = 0;
        // need king (5/11)
        // need knight(2/8)
        // need pawns(0/6)
        // need sliders(4/10, 3/9, 1/7)
        /*
        num squares white can attack - num squares black can attack
        */
        for(int i = 0; i < 6; i++){
            PieceList white = pieces[i];
            PieceList black = pieces[i + 6];

            for(int j = 0; j < white.Count; j++){
                Piece p = white[j];
                switch(p.PieceType){
                    case PieceType.King:
                        whiteMovesBb |= BitboardHelper.GetKingAttacks(p.Square);
                        break;
                    case PieceType.Knight:
                        whiteMovesBb |= BitboardHelper.GetKnightAttacks(p.Square);
                        break;
                    case PieceType.Pawn:
                        whiteMovesBb |= BitboardHelper.GetPawnAttacks(p.Square, true);
                        break;
                    default: // sliders
                        whiteMovesBb |= BitboardHelper.GetSliderAttacks(p.PieceType, p.Square, board);
                        break;
                }
            }

            for(int j = 0; j < black.Count; j++){
                Piece p = black[j];
                switch(p.PieceType){
                    case PieceType.King:
                        blackMovesBb |= BitboardHelper.GetKingAttacks(p.Square);
                        break;
                    case PieceType.Knight:
                        blackMovesBb |= BitboardHelper.GetKnightAttacks(p.Square);
                        break;
                    case PieceType.Pawn:
                        blackMovesBb |= BitboardHelper.GetPawnAttacks(p.Square, false);
                        break;
                    default: // sliders
                        blackMovesBb |= BitboardHelper.GetSliderAttacks(p.PieceType, p.Square, board);
                        break;
                }
            }
        }
        score += (BitboardHelper.GetNumberOfSetBits(whiteMovesBb) - BitboardHelper.GetNumberOfSetBits(blackMovesBb)) * 0.1f; // weighted mobility addition - mobility is good to develop early

        // capture potential
        float captureScore = 0f;
        foreach(Move m in board.GetLegalMoves(true)){
            captureScore += pieceValueLookup[m.CapturePieceType];
        }
        score += (board.IsWhiteToMove ? captureScore : -captureScore) * 1.5f;// + (board.PlyCount * 0.1f); // captures are more important later on

        // check/mate
        if(board.IsInCheckmate()){
            score += board.IsWhiteToMove ? float.NegativeInfinity : float.PositiveInfinity;
        }

        return perspective ? score : -score; // positive good, negative bad!
    }

    // iterative version?
    float AlphaBeta(Board board, int depth, float a, float b, bool isMaximizing, bool perspective){
        Move[] moves = board.GetLegalMoves();
        if(depth == 0 || moves.Length == 0){
            return Evaluate(board, perspective);
        }
        float value;
        if(isMaximizing){
            value = float.NegativeInfinity;
            foreach(Move move in moves){
                board.MakeMove(move);
                value = MathF.Max(value, AlphaBeta(board, depth - 1, a, b, false, perspective)); 
                board.UndoMove(move);
                if(value > b) break;
                a = MathF.Max(a, value);
            }
            return value;
        }
        else{
            value = float.PositiveInfinity;
            foreach(Move move in moves){
                board.MakeMove(move);
                value = MathF.Min(value, AlphaBeta(board, depth - 1, a, b, true, perspective));
                board.UndoMove(move);
                if(value < a) break;
                b = MathF.Min(b, value);
            }
            return value;
        }
    }
}