using ChessChallenge.API;
using System.Collections.Generic;
using System;

public class MyBot : IChessBot
{
    public static Dictionary<PieceType, float> pieceValueLookup = new Dictionary<PieceType, float>() // lookups for piece values
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

        float bestScore = float.NegativeInfinity;
        foreach(Move m in allMoves){
            board.MakeMove(m);
            float thisScore = Evaluate(board, color);
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

    // evaluate with negamax - scored relative to side to move
    public float Evaluate(Board board, bool whiteToMove){
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

        // figure out which squares each side can move to
        ulong whiteAttacks = 0;
        ulong blackAttacks = 0;

        // need king (6/11)
        // need knight(2/8)
        // need pawns(0/6)
        // need sliders(4/10, 3/9, 1/7)

        return whiteToMove ? score : -score; // positive good, negative bad!
    }
}