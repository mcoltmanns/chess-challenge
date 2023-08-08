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

    static Dictionary<ulong, Tuple<float, float>> stateInfo = new Dictionary<ulong, Tuple<float, float>>();

    public Move Think(Board board, Timer timer)
    {
        Move[] allMoves = board.GetLegalMoves();
        bool color = board.IsWhiteToMove;

        // Pick a random move to play if nothing better is found
        Random rng = new();
        Move moveToPlay = allMoves[rng.Next(allMoves.Length)];

        // deep searches are less important with fewer pieces on the board
        int searchDepth = 2; // actually deep searches might be better towards the end - current algorithm tends to get into loops
        // or somehow need to become more aggressive towards the end
        // when is endgame? how to be more aggressive? look for checks/mates and always play them

        float smallestDiff = float.PositiveInfinity;
        float bestOutcome = float.NegativeInfinity;
        float worstOutcome = float.PositiveInfinity;
        foreach(Move m in allMoves){
            board.MakeMove(m);
            Console.Write("searching to a depth of " + searchDepth + "... ");
            float best = FindBestOutcome(board, searchDepth, color);
            if(best >= bestOutcome){
                float worst = FindWorstOutcome(board, searchDepth, color);
                float diff = best - worst;
                Console.WriteLine("doing " + m.ToString() + " has a score difference of " + best + " - " + worst + " = " + diff);
                if(diff <= smallestDiff){ // want to minimize difference, and maximize best outcome
                    smallestDiff = diff;
                    bestOutcome = best;
                    worstOutcome = worst;
                    moveToPlay = m;
                }
            }
            board.UndoMove(m);
        }

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
        score += material * 1.25f; // weight all material

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
        score += (BitboardHelper.GetNumberOfSetBits(whiteMovesBb) - BitboardHelper.GetNumberOfSetBits(blackMovesBb)) * 0.2f; // weighted mobility addition - mobility is good to develop early

        // capture potential
        float captureScore = 0f;
        foreach(Move m in board.GetLegalMoves(true)){
            captureScore += pieceValueLookup[m.CapturePieceType];
        }
        score += (board.IsWhiteToMove ? captureScore : -captureScore) * 1f;// + (board.PlyCount * 0.1f); // captures are more important later on

        return perspective ? score : -score; // positive good, negative bad!
    }

    // iterative versions?
    float FindBestOutcome(Board board, int currentDepth, bool perspective){
        float best = float.NegativeInfinity;
        if(currentDepth == 0) return Evaluate(board, perspective);
        foreach(Move move in board.GetLegalMoves()){
            board.MakeMove(move); // can't be sure what move the opponent might make, so better to check all of them
            ulong key = board.ZobristKey;
            float score;
            if(stateInfo.ContainsKey(key)) score = stateInfo[key].Item1;
            else score = FindBestOutcome(board, currentDepth - 1, perspective);
            board.UndoMove(move);
            if(score > best) best = score;
        }
        return best;
    }

    float FindWorstOutcome(Board board, int currentDepth, bool perspective){
        if(currentDepth == 0) return Evaluate(board, perspective);
        float worst = float.PositiveInfinity;
        foreach(Move move in board.GetLegalMoves()){
            board.MakeMove(move);
            ulong key = board.ZobristKey;
            float score;
            if(stateInfo.ContainsKey(key)) score = stateInfo[key].Item2;
            else score = FindWorstOutcome(board, currentDepth - 1, perspective);
            board.UndoMove(move);
            if(score < worst) worst = score;
        }
        return worst;
    }
}