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
        {PieceType.King, 10f}
    };

    public Move Think(Board board, Timer timer)
    {
        Move[] allMoves = board.GetLegalMoves();
        bool color = board.IsWhiteToMove;

        // Pick a random move to play if nothing better is found
        Random rng = new();
        Move moveToPlay = allMoves[rng.Next(allMoves.Length)];

        // deep searches are less important with fewer pieces on the board
        int searchDepth = (int)Math.Ceiling(BitboardHelper.GetNumberOfSetBits(board.AllPiecesBitboard) * 0.2);

        float bestScore = float.NegativeInfinity;
        foreach(Move m in allMoves){
            board.MakeMove(m);
            float thisScore;// = Evaluate(board, color);
            Console.Write("searching to a depth of " + searchDepth + "... ");
            thisScore = FindBestOutcome(board, 2, color);
            board.UndoMove(m);
            Console.WriteLine("doing " + m.ToString() + " results in score " + thisScore);
            if(thisScore > bestScore){
                bestScore = thisScore;
                moveToPlay = m;
            }
        }

        if(bestScore < 0) return allMoves[rng.Next(allMoves.Length)];

        return moveToPlay;
    }

    // evaluate with negamax - scored relative to side specified
    float Evaluate(Board board, bool perspective){
        // all evaluations in here are done from white's side, with positive good/negative bad. then depending on perspective, multiply by -1
        // piece lists
        PieceList[] pieces = board.GetAllPieceLists(); // wp(0), wkn(1), wb(2), wr(3), wq(4), wk(5), bp(6), bkn(7), bb(8), br(9), bq(10), bk(11)

        float score = 0f;

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
        score += (BitboardHelper.GetNumberOfSetBits(whiteMovesBb) - BitboardHelper.GetNumberOfSetBits(blackMovesBb)) * 0.1f; // weighted mobility addition

        // should consider capture potential?
        float captureScore = 0f;
        foreach(Move m in board.GetLegalMoves(true)){
            captureScore += pieceValueLookup[m.CapturePieceType];
        }
        score += (board.IsWhiteToMove ? captureScore : -captureScore) * 1f;

        return perspective ? score : -score; // positive good, negative bad!
    }

    float FindBestOutcome(Board state, int currentDepth, bool perspective){
        if(currentDepth == 0) return Evaluate(state, perspective);
        float best = float.NegativeInfinity;
        foreach(Move move in state.GetLegalMoves()){
            state.MakeMove(move);
            float score = FindBestOutcome(state, currentDepth - 1, perspective);
            state.UndoMove(move);
            if(score > best) best = score;
        }
        return best;
    }
}