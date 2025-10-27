using System.Collections.Generic;
using UnityEngine;

public class Knight : ChessPieces
{
    public override List<Vector2Int> GetAvailableMoves(ref ChessPieces[,] board, int tileCountX, int tileCountY) {
        // all L shaped knight movements
        List<Vector2Int> r = new List<Vector2Int>();

        int x, y;

        // top right

        x = currentX + 1;
        y = currentY + 2;

        if (x < tileCountX && y < tileCountY)
            if(board[x,y] == null || board[x,y].team != team)
                r.Add(new Vector2Int(x, y));

        x = currentX + 2;
        y = currentY + 1;

        if (x < tileCountX && y < tileCountY)
            if(board[x,y] == null || board[x,y].team != team)
                r.Add(new Vector2Int(x, y));

        // top left

        x = currentX - 1;
        y = currentY + 2;

        if(x >= 0 && y < tileCountY)
            if(board[x,y] == null || board[x,y].team != team)
                r.Add(new Vector2Int(x,y));

        x = currentX - 2;
        y = currentY + 1;

        if(x >= 0 && y < tileCountY)
            if(board[x,y] == null || board[x,y].team != team)
                r.Add(new Vector2Int(x,y));


        // bottomn right
        x = currentX + 1;
        y = currentY -2;

        if(x < tileCountX && y >= 0)
            if (board[x,y] == null || board[x,y].team != team)
                r.Add(new Vector2Int(x,y));

        x = currentX + 2;
        y = currentY - 1;

        if(x < tileCountX && y >= 0)
            if (board[x,y] == null || board[x,y].team != team)
                r.Add(new Vector2Int(x,y));

        // bottom left

        x = currentX - 1;
        y = currentY - 2;

        if(x >= 0 && y >= 0)
            if (board[x,y] == null || board[x,y].team != team)
                r.Add(new Vector2Int(x,y));

        x = currentX - 2;
        y = currentY - 1;

        if(x >= 0 && y >= 0)
            if (board[x,y] == null || board[x,y].team != team)
                r.Add(new Vector2Int(x,y));





        return r;
    }
}
