using System.Collections.Generic;
using UnityEngine;

public class Pawn : ChessPieces
{
    public override List<Vector2Int> GetAvailableMoves(ref ChessPieces[,] board, int tileCountX, int tileCountY) {
        // all pawn movements
        List<Vector2Int> r = new List<Vector2Int>();

        int direction = (team == 0) ? 1 : -1;

        // one in front

        if(board[currentX, currentY + direction] == null)
            r.Add(new Vector2Int(currentX, currentY + direction)); 

        // two in front
        if(board[currentX, currentY + direction] == null) {

            // white team
            if (team == 0 && currentY == 1 && board[currentX, currentY + (direction * 2)] == null)
                r.Add(new Vector2Int(currentX, currentY + (direction*2)));
        

            if (team == 1 && currentY == 6 && board[currentX, currentY + (direction * 2)] == null)
                r.Add(new Vector2Int(currentX, currentY + (direction*2)));
        }

        // kill move

        if(currentX != tileCountX-1)
            if(board[currentX+1, currentY+direction] != null && board[currentX+1, currentY + direction].team != team)
                r.Add(new Vector2Int(currentX+1, currentY+ direction));

        if(currentX != 0)
            if(board[currentX-1, currentY+direction] != null && board[currentX-1, currentY + direction].team != team)
                r.Add(new Vector2Int(currentX-1, currentY+ direction));

        

        return r;
    }

    public override SpecialMove GetSpecialMoves(ref ChessPieces[,] board, ref List<Vector2Int[]> moveList, ref List<Vector2Int> availableMoves) {
        // en passant movement check to execute it on board 
        
        int direction = (team == 0) ? 1 : -1;

        if((team == 0 && currentY == 6) || (team == 1 && currentY == 1)) {
            return SpecialMove.Promotion;
        }
        // en passant
        if(moveList.Count > 0) {
            Vector2Int[] lastMove = moveList[moveList.Count - 1];

            if(board[lastMove[1].x, lastMove[1].y].type == ChessPieceType.Pawn) { // if last piece moved was a pawn
                if(Mathf.Abs(lastMove[0].y - lastMove[1].y) == 2) { // if last move was a +2 in either direction
                    if (board[lastMove[1].x, lastMove[1].y].team != team)  { // if last move was from other team
                        if(lastMove[1].y == currentY) { // if both pawns are on same y
                            if(lastMove[1].x == currentX - 1) { // landed left
                                availableMoves.Add(new Vector2Int(currentX - 1, currentY + direction));
                                return SpecialMove.EnPassant;
                            }
                            
                            if(lastMove[1].x == currentX + 1) { // landed right
                                availableMoves.Add(new Vector2Int(currentX + 1, currentY + direction));
                                return SpecialMove.EnPassant;
                            }
                        }
                    }
                }
            }
        }
        
        return SpecialMove.None;
    }
}
