using System.Collections;
using System;
using System.Collections.Generic;
using Unity.Networking.Transport;
using Unity.Collections;
using UnityEngine.UI;
using UnityEngine;

public enum SpecialMove {
    None = 0,
    EnPassant,
    Castling,
    Promotion
}
public class Chess : MonoBehaviour
{

    [Header("Designs")]
    [SerializeField] private Material tileMaterial;
    [SerializeField] private float tileSize = 1.0f;
    [SerializeField] private float yOffset = 0.2f;
    [SerializeField] private Vector3 boardCenter = Vector3.zero;
    [SerializeField] private float deathSize = 0.3f;
    [SerializeField] private float deathSpacing = 0.3f;
    [SerializeField] private float dragOffset = 1.5f;
    [SerializeField] private GameObject victoryScreen;
    [SerializeField] private Transform rematchIndicator;
    [SerializeField] private Button rematchButton;

    [Header("Prefabs & Materials")]
    [SerializeField] private GameObject[] prefabs;
    [SerializeField] private Material[] teamMaterials;


    // LOGIC 
    private ChessPieces[,] chessPieces;
    private ChessPieces currentlyDragging;
    private List<Vector2Int> availableMoves = new List<Vector2Int>();
    private List<ChessPieces> deadWhites = new List<ChessPieces>();
    private List<ChessPieces> deadBlacks = new List<ChessPieces>();
    private const int countX = 8;
    private const int countY = 8;
    private GameObject[,] tiles;
    private Camera currentCamera;
    private Vector2Int currentHover;
    private Vector3 bounds;
    private bool isWhiteTurn;
    private SpecialMove specialMove;
    private List<Vector2Int[]> moveList = new List<Vector2Int[]>();

// multi logic
    private int playerCount = -1;
    private int currentTeam = -1;
    private bool localGame = true;
    private bool[] playerRematch = new bool[2];




    private void Start() {        

        isWhiteTurn = true;

        MakeTiles(tileSize, countX, countY);
        SpawnAllPieces();
        PositionAllPieces();

        RegisterEvents();
    }

    private void Update() {

        if(!currentCamera) {
            currentCamera = Camera.main;
            return;
        }

        RaycastHit info;
        Ray ray = currentCamera.ScreenPointToRay(Input.mousePosition);

        // if we are hovering a tile after not hovering any tiles
        if(Physics.Raycast(ray, out info, 100, LayerMask.GetMask("Tile", "Hover", "Highlight"))) {

            // get indexes of hit tiles
            Vector2Int hitPosition = LookupTileIndex(info.transform.gameObject);

            
            if (currentHover == -Vector2Int.one) {
                currentHover = hitPosition;
                tiles[hitPosition.x, hitPosition.y].layer = 9; //LayerMask.NameToLayer("Hover")
            }

            if (currentHover != hitPosition) {
                tiles[currentHover.x, currentHover.y].layer = (ContainsValidMove(ref availableMoves, currentHover)) ? LayerMask.NameToLayer("Highlight") : LayerMask.NameToLayer("Tile"); //LayerMask.NameToLayer("Tile")
                currentHover = hitPosition;
                tiles[hitPosition.x, hitPosition.y].layer = 9; //LayerMask.NameToLayer("Hover")
                currentHover = hitPosition;
            }
            
            // if we press down on the mouse
            if(Input.GetMouseButtonDown(0)) {
                if(chessPieces[hitPosition.x, hitPosition.y] != null) {
                    // is it our turn?
                    if ((chessPieces[hitPosition.x, hitPosition.y].team == 0 && isWhiteTurn && currentTeam == 0) || (chessPieces[hitPosition.x, hitPosition.y].team == 1 && !isWhiteTurn && currentTeam == 1)) {
                        currentlyDragging = chessPieces[hitPosition.x, hitPosition.y];

                        // get a list of where i can go, highlight tiles as well
                        availableMoves = currentlyDragging.GetAvailableMoves(ref chessPieces, countX, countY);

                        // get a list of special moves
                        specialMove = currentlyDragging.GetSpecialMoves(ref chessPieces, ref moveList, ref availableMoves);

                        PreventCheck();
                        HighlightTiles();

                    }
                }
            }

             // if we are releasing the mouse   
            if(currentlyDragging != null && Input.GetMouseButtonUp(0)) {

                Vector2Int previousPosition = new Vector2Int(currentlyDragging.currentX, currentlyDragging.currentY);
                
                if(ContainsValidMove(ref availableMoves, new Vector2Int(hitPosition.x, hitPosition.y))) {
                    MoveTo(previousPosition.x, previousPosition.y, hitPosition.x, hitPosition.y);

                    // net implementation
                    NetMakeMove mm = new NetMakeMove();
                    mm.originalX = previousPosition.x;
                    mm.originalY = previousPosition.y;
                    mm.destinationX = hitPosition.x;
                    mm.destinationY = hitPosition.y;
                    mm.teamId = currentTeam;
                    Client.Instance.SendToServer(mm);

                }
                else {
                    currentlyDragging.SetPosition(GetTileCenter(previousPosition.x, previousPosition.y));
                    currentlyDragging = null;
                    RemoveHighlightTiles();
                
                }             
    
            }

        }
        else {
            if(currentHover != -Vector2Int.one) {
                tiles[currentHover.x, currentHover.y].layer = (ContainsValidMove(ref availableMoves, currentHover)) ? LayerMask.NameToLayer("Highlight") : LayerMask.NameToLayer("Tile");
                currentHover = -Vector2Int.one;
            }

            if (currentlyDragging && Input.GetMouseButtonUp(0)) {
                currentlyDragging.SetPosition(GetTileCenter(currentlyDragging.currentX, currentlyDragging.currentY));
                currentlyDragging = null;
                RemoveHighlightTiles();
            }
        }

        // if we're dragging a piece

        if(currentlyDragging) {
            Plane horizontalPlane = new Plane(Vector3.up, Vector3.up*yOffset);
            float distance = 0.0f;
            if (horizontalPlane.Raycast(ray, out distance))
                currentlyDragging.SetPosition(ray.GetPoint(distance) * dragOffset);
        }

    }

// Generate the board
    public void MakeTiles(float tileSize, int tileCountX, int tileCountY) {

        yOffset += transform.position.y;
        bounds = new Vector3((tileCountX /2 ) * tileSize, 0, (tileCountX/2) * tileSize) + boardCenter;

        tiles = new GameObject[countX, countY];
        for (int outerIterator = 0; outerIterator < tileCountX; outerIterator++) {
            for (int innerIterator = 0; innerIterator < tileCountY; innerIterator++) {
                tiles[outerIterator, innerIterator] = MakeSingleTile(tileSize, outerIterator, innerIterator);
            }
        }

    }

    private GameObject MakeSingleTile(float tileSize, int x, int y) {
    

        GameObject tileObject = new GameObject(string.Format("X:{0}, Y:{1}", x, y));
        tileObject.transform.parent = transform;

        Mesh mesh = new Mesh();
        tileObject.AddComponent<MeshFilter>().mesh = mesh;
        tileObject.AddComponent<MeshRenderer>().material = tileMaterial;

        Vector3[] vertices = new Vector3[4];
        vertices[0] = new Vector3(x * tileSize, yOffset, y * tileSize) - bounds;
        vertices[1] = new Vector3(x * tileSize, yOffset, (y+1) * tileSize) - bounds;
        vertices[2] = new Vector3((x+1) * tileSize, yOffset, y * tileSize) - bounds;
        vertices[3] = new Vector3((x+1) * tileSize, yOffset, (y+1) * tileSize) - bounds;

        int[] triangles = new int[] {0, 1, 2, 1, 3, 2}; 

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();

        tileObject.layer = LayerMask.NameToLayer("Tile");
        tileObject.AddComponent<BoxCollider>(); // more effecient than plane
        
        return tileObject;


        }

    // Spawning pieces

    private void SpawnAllPieces() {
        chessPieces = new ChessPieces[countX, countY];

        int whiteTeam = 0, blackTeam = 1;

        // White team
        chessPieces[0,0] = SpawnSinglePiece(ChessPieceType.Rook, whiteTeam);
        chessPieces[1, 0] = SpawnSinglePiece(ChessPieceType.Knight, whiteTeam);
        chessPieces[2, 0] = SpawnSinglePiece(ChessPieceType.Bishop, whiteTeam);
        chessPieces[4, 0] = SpawnSinglePiece(ChessPieceType.King, whiteTeam);
        chessPieces[3, 0] = SpawnSinglePiece(ChessPieceType.Queen, whiteTeam);
        chessPieces[5, 0] = SpawnSinglePiece(ChessPieceType.Bishop, whiteTeam);
        chessPieces[6, 0] = SpawnSinglePiece(ChessPieceType.Knight, whiteTeam);
        chessPieces[7,0] = SpawnSinglePiece(ChessPieceType.Rook, whiteTeam);

        pawnSpawn(chessPieces, whiteTeam, 0, 1); 

        // Black team
        chessPieces[0, 7] = SpawnSinglePiece(ChessPieceType.Rook, blackTeam);
        chessPieces[1, 7] = SpawnSinglePiece(ChessPieceType.Knight, blackTeam);
        chessPieces[2, 7] = SpawnSinglePiece(ChessPieceType.Bishop, blackTeam);
        chessPieces[4, 7] = SpawnSinglePiece(ChessPieceType.King, blackTeam);
        chessPieces[3, 7] = SpawnSinglePiece(ChessPieceType.Queen, blackTeam);
        chessPieces[5, 7] = SpawnSinglePiece(ChessPieceType.Bishop, blackTeam);
        chessPieces[6, 7] = SpawnSinglePiece(ChessPieceType.Knight, blackTeam);
        chessPieces[7, 7] = SpawnSinglePiece(ChessPieceType.Rook, blackTeam);

        pawnSpawn(chessPieces, blackTeam, 0, 6);

    }

    private void pawnSpawn(ChessPieces[,] pieces, int team, int count, int placement) { // recursion
        if (count == 7) {
            pieces[7, placement] = SpawnSinglePiece(ChessPieceType.Pawn, team);
        }
        else {
            pieces[count, placement] = SpawnSinglePiece(ChessPieceType.Pawn, team);
            pawnSpawn(pieces, team, count+1, placement);
        }
        
    }

    private ChessPieces SpawnSinglePiece(ChessPieceType type, int team) {
        ChessPieces cp = Instantiate(prefabs[(int)type - 1], transform).GetComponent<ChessPieces>();

        cp.type = type;
        cp.team = team;
        cp.GetComponent<MeshRenderer>().material = teamMaterials[team];
        return cp;
    }

    // Positioning
    private void PositionAllPieces() {
        for (int x = 0; x < countX; x++ ) {
            for (int y = 0; y < countY; y++) {
                if (chessPieces[x, y] != null)
                    PositionSinglePiece(x, y, true);
            }
        }
    }

    // moving a piece to where it needs to go
    private void PositionSinglePiece(int x, int y, bool force = false) {
        chessPieces[x, y].currentX = x;
        chessPieces[x, y].currentY = y;
        chessPieces[x, y].SetPosition(GetTileCenter(x, y));
    }

    private Vector3 GetTileCenter(int x, int y) {
        return new Vector3(x*tileSize, yOffset, y*tileSize) - bounds + new Vector3(tileSize/2, 0, tileSize/2);
    }

// Highlight Tiles

    private void HighlightTiles() {
        for (int i = 0; i < availableMoves.Count; i++)
            tiles[availableMoves[i].x, availableMoves[i].y].layer = LayerMask.NameToLayer("Highlight");
    }

    private void RemoveHighlightTiles() {
        for (int i = 0; i < availableMoves.Count; i++)
            tiles[availableMoves[i].x, availableMoves[i].y].layer = LayerMask.NameToLayer("Tile");

        availableMoves.Clear();
    }

// Checkmate

    private void CheckMate(int team) {
        DisplayVictory(team);
    }
    private void DisplayVictory(int winningTeam) {

        victoryScreen.SetActive(true);
        victoryScreen.transform.GetChild(winningTeam +2).gameObject.SetActive(true);

    }

    public void OnRematchButton() {

// rematch mechanics, checks to see which team wants a rematch
        if(localGame) {
            NetRematch wrm = new NetRematch();
            wrm.teamId = 0;
            wrm.wantRematch = 1;
            Client.Instance.SendToServer(wrm);

            NetRematch brm = new NetRematch();
            brm.teamId = 1;
            brm.wantRematch = 1;
            Client.Instance.SendToServer(brm);
        }
        else {
            NetRematch rm = new NetRematch();
            rm.teamId = currentTeam;
            rm.wantRematch = 1;
            Client.Instance.SendToServer(rm);

        }

    }

    public void GameReset() {
        // UI

        // basically turning on and off the rematch indicator
        rematchButton.interactable = true;

        rematchIndicator.transform.GetChild(2).gameObject.SetActive(false);
        rematchIndicator.transform.GetChild(3).gameObject.SetActive(false);

        victoryScreen.transform.GetChild(2).gameObject.SetActive(false);
        victoryScreen.transform.GetChild(3).gameObject.SetActive(false);
        victoryScreen.SetActive(false);

        // fields reset

        currentlyDragging = null;
        availableMoves.Clear();
        moveList.Clear();
        playerRematch[0] = playerRematch[1] = false;

        // Clean up
        for (int x = 0; x < countX; x++) {
            for (int y = 0; y < countY; y++) {
                if(chessPieces[x,y] != null)
                    Destroy(chessPieces[x, y].gameObject);

                chessPieces[x,y]= null;
            }
        }

        for (int i = 0; i < deadWhites.Count; i++) 
            Destroy(deadWhites[i].gameObject);

        for (int i = 0; i < deadBlacks.Count; i++)
            Destroy(deadBlacks[i].gameObject);

        deadWhites.Clear();
        deadBlacks.Clear();

        SpawnAllPieces();
        PositionAllPieces();
        isWhiteTurn = true;

    }

    public void OnMenuButton() {
        // reset for when you leave game
        NetRematch rm = new NetRematch();
        rm.teamId = currentTeam;
        rm.wantRematch = 0;
        Client.Instance.SendToServer(rm);

        GameReset();
        GameUI.Instance.OnLeaveFromGameMenu();

        Invoke("ShutdownRelay", 1.0f);

        // reset some values
        playerCount = -1;
        currentTeam = -1;

    }

// Special Moves

    private void SortDead(List<ChessPieces> pieces) {
        // sorting dead pieces using bubble sort

        List<int> toSort = new List<int>();

        for(int i = 0; i < pieces.Count; i++) {
            if(pieces[i].type == ChessPieceType.Pawn)
                toSort.Add(1);
            if(pieces[i].type == ChessPieceType.Rook)
                toSort.Add(2);
            if(pieces[i].type == ChessPieceType.Knight)
                toSort.Add(3);
            if(pieces[i].type == ChessPieceType.Bishop)
                toSort.Add(4);
            if(pieces[i].type == ChessPieceType.Queen)
                toSort.Add(5);
        }

        for (int b = 0; b < toSort.Count -1; b++) {
            for(int a = 0; a < toSort.Count-1; a++) {
                if(toSort[a] > toSort[a+1]) {
                    var tempVar = toSort[a];
                    toSort[a] = toSort[a+1];
                    toSort[a+1] = tempVar;

                    ChessPieces tempPiece = pieces[a];
                    pieces[a] = pieces[a+1];
                    pieces[a+1] = tempPiece;
                }
            }
        }

    }

    private void ProcessSpecialMove() {
        // if something dies it must be moved off to the side of the board
        if (specialMove == SpecialMove.EnPassant) {
            var newMove = moveList[moveList.Count - 1];
            ChessPieces myPawn = chessPieces[newMove[1].x, newMove[1].y];
            var targetPawnPosition = moveList[moveList.Count - 2];
            ChessPieces enemyPawn = chessPieces[targetPawnPosition[1].x, targetPawnPosition[1].y];

            if(myPawn.currentX == enemyPawn.currentX) {
                if(myPawn.currentY == enemyPawn.currentY - 1 || myPawn.currentY == enemyPawn.currentY + 1) {
                    if(enemyPawn.team == 0) {
                        deadWhites.Add(enemyPawn);
                        SortDead(deadWhites);
                        enemyPawn.SetScale(Vector3.one*deathSize);
                        enemyPawn.SetPosition(new Vector3(8 * tileSize, yOffset, -1*tileSize) - bounds + new Vector3(tileSize / 2, 0, tileSize / 2) + (Vector3.forward*deathSpacing) * deadWhites.Count);
                    }
                    else {
                        deadBlacks.Add(enemyPawn);
                        SortDead(deadBlacks);
                        enemyPawn.SetScale(Vector3.one*deathSize);
                        enemyPawn.SetPosition(new Vector3(8 * tileSize, yOffset, -1*tileSize) - bounds + new Vector3(tileSize / 2, 0, tileSize / 2) + (Vector3.forward*deathSpacing) * deadBlacks.Count);
                    }

                    chessPieces[enemyPawn.currentX, enemyPawn.currentY] = null; 
                    
                }
            }

        }

        if(specialMove == SpecialMove.Promotion) {
            // if promoting a piece, you must destroy the pawn and position the queen
            Vector2Int[] lastMove = moveList[moveList.Count-1];
            ChessPieces targetPawn = chessPieces[lastMove[1].x, lastMove[1].y];

            if(targetPawn.type == ChessPieceType.Pawn) {
                if(targetPawn.team == 0 && lastMove[1].y == 7) {
                    ChessPieces newQueen = SpawnSinglePiece(ChessPieceType.Queen, 0);
                    newQueen.transform.position = chessPieces[lastMove[1].x, lastMove[1].y].transform.position;
                    Destroy(chessPieces[lastMove[1].x, lastMove[1].y].gameObject);
                    chessPieces[lastMove[1].x, lastMove[1].y] = newQueen;
                    PositionSinglePiece(lastMove[1].x, lastMove[1].y);
                }

                if(targetPawn.team == 1 && lastMove[1].y == 0) {
                    ChessPieces newQueen = SpawnSinglePiece(ChessPieceType.Queen, 1);
                    newQueen.transform.position = chessPieces[lastMove[1].x, lastMove[1].y].transform.position;
                    Destroy(chessPieces[lastMove[1].x, lastMove[1].y].gameObject);
                    chessPieces[lastMove[1].x, lastMove[1].y] = newQueen;
                    PositionSinglePiece(lastMove[1].x, lastMove[1].y);
                }
            }

        }

        if(specialMove == SpecialMove.Castling) {
            // moving rooks around for castling
            Vector2Int[] lastMove = moveList[moveList.Count - 1];

            // left rook
            if(lastMove[1].x == 2) {
                if(lastMove[1].y == 0) { // white side
                    ChessPieces rook = chessPieces[0, 0];
                    chessPieces[3, 0] = rook;
                    PositionSinglePiece(3, 0);
                    chessPieces[0, 0] = null;
                }
                else if (lastMove[1].y == 7) { // black side
                    ChessPieces rook = chessPieces[0, 7];
                    chessPieces[3, 7] = rook;
                    PositionSinglePiece(3, 7);
                    chessPieces[0, 7] = null;
                }
            }
            // right rook
            else if(lastMove[1].x == 6) {
                if(lastMove[1].y == 0) { // white side
                    ChessPieces rook = chessPieces[7, 0];
                    chessPieces[5, 0] = rook;
                    PositionSinglePiece(5, 0);
                    chessPieces[7, 0] = null;
                }
                else if (lastMove[1].y == 7) { // black side
                    ChessPieces rook = chessPieces[7, 7];
                    chessPieces[5, 7] = rook;
                    PositionSinglePiece(5, 7);
                    chessPieces[7, 7] = null;
                }
            }
        }
    }

    private void PreventCheck() {
        // removing tiles to prevent check
        ChessPieces targetKing = null;
        for (int x = 0; x < countX; x++) {
            for (int y = 0; y < countY; y++) {
                if(chessPieces[x,y] != null)
                    if(chessPieces[x,y].type == ChessPieceType.King)
                        if(chessPieces[x,y].team == currentlyDragging.team)
                             targetKing = chessPieces[x,y];
            }
        }
        // since were sending in ref available move we will be deleting moves that are putting us in check
        SimulateMoveForSinglePiece(currentlyDragging, ref availableMoves, targetKing);
    }

    private void SimulateMoveForSinglePiece(ChessPieces cp, ref List<Vector2Int> moves, ChessPieces targetKing) {
        // Save the current values, to reset after function is called

        int actualX = cp.currentX;
        int actualY = cp.currentY;

        List<Vector2Int> movesToRemove = new List<Vector2Int>();
    
        // going through all moves, simulate them, and check if we are in check

        for(int i = 0; i < moves.Count; i++) {
            int simX = moves[i].x;
            int simY = moves[i].y;

            Vector2Int kingPositionThisSim = new Vector2Int(targetKing.currentX, targetKing.currentY);
            // did we simulate the king move
            if(cp.type == ChessPieceType.King)
                kingPositionThisSim = new Vector2Int(simX, simY);

            // copy the [,] and NOT a ref
            ChessPieces[,] simulation = new ChessPieces[countX, countY];
            List<ChessPieces> simAttackingPieces = new List<ChessPieces>();

            for(int x = 0; x < countX; x++) {
                for (int y = 0; y < countY; y++) {
                    if(chessPieces[x,y] != null) {
                        simulation[x,y] = chessPieces[x,y];
                        if(simulation[x,y].team != cp.team) 
                            simAttackingPieces.Add(simulation[x,y]);
                    }
                }
            }

            // simulate move
            simulation[actualX, actualY] = null;
            cp.currentX = simX;
            cp.currentY = simY;
            simulation[simX, simY] = cp;

            // did one of the pieces get taken down during our sim
            var deadPiece = simAttackingPieces.Find(c => c.currentX == simX && c.currentY == simY);

            if(deadPiece != null)
                simAttackingPieces.Remove(deadPiece);

            // get all simulated attacking pieces move

            List<Vector2Int> simMoves = new List<Vector2Int>();

            for(int a = 0; a < simAttackingPieces.Count; a++) {
                var pieceMoves = simAttackingPieces[a].GetAvailableMoves(ref simulation, countX, countY);
                for (int b = 0; b < pieceMoves.Count; b++) 
                    simMoves.Add(pieceMoves[b]);
                
            }

            // is the king in trouble? if so remove the move
            if(ContainsValidMove(ref simMoves, kingPositionThisSim)) {
                movesToRemove.Add(moves[i]);
            }

            // restore actual cp data
            cp.currentX = actualX;
            cp.currentY = actualY;
        }

        // remove from the current available move list
        for(int i = 0; i < movesToRemove.Count; i++) {
            moves.Remove(movesToRemove[i]);
        }
    }

    private bool CheckForCheckmate() {
        var lastMove = moveList[moveList.Count-1];
        int targetTeam = (chessPieces[lastMove[1].x, lastMove[1].y].team == 0) ? 1 : 0;

        List<ChessPieces> attackingPieces = new List<ChessPieces>();
        List<ChessPieces> defendingPieces = new List<ChessPieces>();
        ChessPieces targetKing = null;

        for(int x = 0; x < countX; x++)
            for(int y = 0; y < countY; y++)
                if(chessPieces[x,y] != null) {
                    if(chessPieces[x,y].team == targetTeam) {
                        defendingPieces.Add(chessPieces[x,y]);
                        if(chessPieces[x,y].type == ChessPieceType.King)
                            targetKing = chessPieces[x,y];
                    }
                    else {
                        attackingPieces.Add(chessPieces[x,y]);
                    }
                }
        // is the king attacked right now?
        List<Vector2Int> currentAvailableMoves = new List<Vector2Int>();
        for (int i = 0; i < attackingPieces.Count; i++) {
            var pieceMoves = attackingPieces[i].GetAvailableMoves(ref chessPieces, countX, countY);
            for (int b = 0; b < pieceMoves.Count; b++)
                currentAvailableMoves.Add(pieceMoves[b]);
        }

        // are we in check rn
        if(ContainsValidMove(ref currentAvailableMoves, new Vector2Int(targetKing.currentX, targetKing.currentY))) {
            // king is under attack, can we move smth to help him?
            for(int i = 0; i < defendingPieces.Count; i++) {
                List<Vector2Int> defendingMoves = defendingPieces[i].GetAvailableMoves(ref chessPieces, countX, countY);
                // since were sending ref availableMoves we wil be deleting moves that are putting us in check
                SimulateMoveForSinglePiece(defendingPieces[i], ref defendingMoves, targetKing);

                if(defendingMoves.Count != 0)
                    return false;
            }

            return true; // checkmate exit 
        }

        return false;
    }


// Operations

    private bool ContainsValidMove(ref List<Vector2Int> moves, Vector2Int pos) {
        for (int i = 0; i < moves.Count; i++) {
            if(moves[i].x == pos.x && moves[i].y == pos.y)
                return true;
        }
        return false;
    }

    private void MoveTo(int originalX, int originalY, int x, int y) {

        ChessPieces cp = chessPieces[originalX, originalY];

        // is there another piece on target position

        Vector2Int previousPosition = new Vector2Int(originalX, originalY);

        if (chessPieces[x, y] != null) {
            ChessPieces ocp = chessPieces[x, y];

            if (cp.team == ocp.team) {
                return;
            }

                // if it is enemy team

            if (ocp.team == 0) {

                if(ocp.type == ChessPieceType.King) {
                    CheckMate(1);
                }

                deadWhites.Add(ocp);
                SortDead(deadWhites);
                ocp.SetScale(Vector3.one*deathSize);
                ocp.SetPosition(new Vector3(8 * tileSize, yOffset, -1*tileSize) - bounds + new Vector3(tileSize / 2, 0, tileSize / 2) + (Vector3.forward*deathSpacing) * deadWhites.Count);
            } 
            
            else {

                if (ocp.type == ChessPieceType.King) {
                    CheckMate(0);
                }

                deadBlacks.Add(ocp);
                SortDead(deadBlacks);
                ocp.SetScale(Vector3.one*deathSize);
                ocp.SetPosition(new Vector3(-1 * tileSize, yOffset, 8*tileSize) - bounds + new Vector3(tileSize / 2, 0, tileSize / 2) + (Vector3.back*deathSpacing) * deadBlacks.Count);
            }

        }

        chessPieces[x, y] = cp;
        chessPieces[previousPosition.x, previousPosition.y] = null;

        PositionSinglePiece(x, y);

        isWhiteTurn = !isWhiteTurn;

        if(localGame)
            currentTeam = (currentTeam == 0) ? 1 : 0;

        moveList.Add(new Vector2Int[] { previousPosition, new Vector2Int(x, y)});

        ProcessSpecialMove();

        if(currentlyDragging)
            currentlyDragging = null;

        RemoveHighlightTiles();

        if(CheckForCheckmate())
            CheckMate(cp.team);

        return;
    }

    private Vector2Int LookupTileIndex(GameObject hitInfo) {

        for (int x = 0; x < countX; x++)
        {
            for (int y = 0; y < countY; y++)
            {
                if (tiles[x,y] == hitInfo) {
                    return new Vector2Int(x, y);
                }
                
            }
        }
        return -Vector2Int.one; // invalid

    }

    private void RegisterEvents() {
        // all possible messages sent through server and client 

        NetUtility.S_WELCOME += OnWelcomeServer;
        NetUtility.S_MAKE_MOVE += OnMakeMoveServer;
        NetUtility.C_MAKE_MOVE += OnMakeMoveClient;
        NetUtility.S_REMATCH += OnRematchServer;
        NetUtility.C_WELCOME += OnWelcomeClient;
        NetUtility.C_START_GAME += OnStartGameClient;
        NetUtility.C_REMATCH += OnRematchClient;

        GameUI.Instance.SetLocalGame += OnSetLocalGame;
    }

    private void UnRegisterEvents() {

        // unregistering server and client messages

        NetUtility.S_WELCOME -= OnWelcomeServer;
        NetUtility.S_MAKE_MOVE -= OnMakeMoveServer;
        NetUtility.S_REMATCH -= OnRematchServer;
        NetUtility.C_WELCOME -= OnWelcomeClient;
        NetUtility.C_MAKE_MOVE -= OnMakeMoveClient;
        NetUtility.C_REMATCH -= OnRematchClient;
        NetUtility.C_START_GAME -= OnStartGameClient;

        GameUI.Instance.SetLocalGame -= OnSetLocalGame;

    }

    // server
    private void OnWelcomeServer(NetMessage msg, NetworkConnection cnn) {
        // client has connected, assign a team and return the message back
        NetWelcome nw = msg as NetWelcome;

        // assign a team 
        nw.AssignedTeam = ++playerCount;

        // return back to client
        Server.Instance.SendToClient(cnn, nw);

        // if full, start the game
        if(playerCount == 1) 
            Server.Instance.Broadcast(new NetStartGame());
        
    }

    private void OnMakeMoveServer(NetMessage msg, NetworkConnection cnn) {
        // recieve and broadcast back

        NetMakeMove mm = msg as NetMakeMove;

        Server.Instance.Broadcast(mm);

    }

    private void OnRematchServer(NetMessage msg, NetworkConnection cnn) {

        Server.Instance.Broadcast(msg);

    }

    // client

    private void OnWelcomeClient(NetMessage msg) {
        // recieve connection message
        NetWelcome nw = msg as NetWelcome;

        // assign team
        currentTeam = nw.AssignedTeam;

        Debug.Log($"My assigned team is {nw.AssignedTeam}");

        if(localGame && currentTeam == 0) 
            Server.Instance.Broadcast(new NetStartGame());
        
    }

    private void OnStartGameClient(NetMessage msg) {
        // change camera
        GameUI.Instance.ChangeCamera((currentTeam == 0) ? CameraAngle.whiteTeam : CameraAngle.blackTeam);
    }

    private void OnMakeMoveClient(NetMessage msg) {
        NetMakeMove mm = msg as NetMakeMove;

        // makes the move for the other person

        Debug.Log($"MM : {mm.teamId} : {mm.originalX} {mm.originalY} -> {mm.destinationX} {mm.destinationY}");

        if(mm.teamId != currentTeam) {

            ChessPieces target = chessPieces[mm.originalX, mm.originalY];

            availableMoves = target.GetAvailableMoves(ref chessPieces, countX, countY);
            specialMove = target.GetSpecialMoves(ref chessPieces, ref moveList, ref availableMoves);

            MoveTo(mm.originalX, mm.originalY, mm.destinationX, mm.destinationY);
        }
    }

    private void OnRematchClient(NetMessage msg) {

        // recieve message
        NetRematch rm = msg as NetRematch;

        // set boolean for rematch
        playerRematch[rm.teamId] = rm.wantRematch == 1;

        // activate UI
        if(rm.teamId != currentTeam)
            rematchIndicator.transform.GetChild((rm.wantRematch == 1) ? 0 : 1).gameObject.SetActive(true);
            if(rm.wantRematch != 1) {
                rematchButton.interactable = false;
            }

        // if both want rematch
        if(playerRematch[0] && playerRematch[1])
            GameReset();
            



    }

    // 

    private void ShutdownRelay() {
        Client.Instance.Shutdown();
        Server.Instance.Shutdown();
    }
    
    private void OnSetLocalGame(bool v) {
        playerCount = -1;
        currentTeam = -1;
        localGame = v;
    }



}