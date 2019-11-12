using UnityEngine;
using System.Linq;
using KMHelper;
using System.Collections;

/// <summary>
/// A maze module that requires the defuser to navigate a grid of logic gates based on a displayed number which changes after each move. 
/// If the displayed number when converted to 2-digit binary, would return a 1 when passed through the logic gate, then the space is a legal move.
/// The gates in the grid are Nor, Xor, Or, and And, and the controls include a display screen which shows the current number to be tested, 
/// 4 movement buttons (U R L D) to move up right left and down respectively in the grid, and a stuck? button which should only be used if the defuser
/// is in a deadend. At this moment the defuser can press the button to change the number in the display so they may move again. If the defuser 
/// tries to move into an illegal square (i.e. the logic gate would return 0) then the defuser gains a strike and does not move. If the defuser attempts
/// to use the Stuck? button when they still have a legal move (even backwards!) then they gain a strike and are reset back to the start. 
/// The starting and ending position are set by the (3rd,4th) and (5th,6th) characters of the serial, in the format of (row,col) where the top left square
/// is (0,0). A = 1, B = 2, C = 3, ... and any letter > 9 should be taken modulo 10 so it stays within the grid. 
/// </summary>
public class boolMaze : MonoBehaviour
{
    public KMBombInfo BombInfo;
    public KMBombModule BombModule;
    public KMAudio KMAudio;
    public KMSelectable ButtonUp;
    public KMSelectable ButtonLeft;
    public KMSelectable ButtonRight;
    public KMSelectable ButtonDown;
    public KMSelectable ButtonStuck;
    public KMSelectable ButtonReset;
    public TextMesh NumDisplay;
    

    //Initialize Variables
    private int gridPosRow = 0;
    private int gridPosCol = 0;
    private int correctgridrow = 0;
    private int correctgridcol = 0;
    private int booldisplay = 0;
    private int initrow = 0;
    private int initcol = 0;
    private bool _isSolved = false;
    private int _moduleId;
    private static int _moduleIdCounter = 1;
    private bool stuck = false;

    //0 = Nor, 1 = Xor, 2 = Or, 3 = And
    private int[,] grid = new int[10,10] {
        { 0, 1, 2, 3, 2, 3, 1, 0, 2, 1 },
        { 1, 3, 2, 0, 2, 2, 2, 3, 1, 3 },
        { 2, 3, 2, 2, 1, 0, 2, 3, 2, 2 },
        { 3, 0, 2, 0, 2, 1, 3, 0, 2, 2 },
        { 2, 2, 3, 2, 2, 0, 2, 2, 0, 1 },
        { 1, 2, 3, 0, 2, 2, 3, 0, 1, 2 },
        { 2, 2, 3, 0, 2, 3, 1, 2, 2, 1 },
        { 1, 3, 2, 2, 2, 1, 0, 0, 2, 2 },
        { 1, 2, 2, 2, 3, 3, 0, 0, 2, 1 },
        { 2, 2, 1, 0, 3, 2, 1, 2, 3, 0 }
    };

    void Start()
    {
        _moduleId = _moduleIdCounter++;
        char[] serial = BombInfo.GetSerialNumber().ToCharArray();

        //Use 3rd/4th characters of serial to calculate starting position
        gridPosRow = ConvToPos(serial[2]);
        gridPosCol = ConvToPos(serial[3]);

        //Save starting position for resets
        initrow = gridPosRow;
        initcol = gridPosCol;

        //Use 5th/6th characters of serial to calculate ending position
        correctgridrow = ConvToPos(serial[4]);
        correctgridcol = ConvToPos(serial[5]);

        //Display a random integer between 0 and 3
        booldisplay = Random.Range(0, 4);
        NumDisplay.text = booldisplay + "";

        //Moves ending off of AND/NOR
        CheckBadEnding();

        //Log stuff
        Debug.LogFormat("[BooleanMaze #{2}] Starting Location:({0},{1})", initrow, initcol, _moduleId);
        Debug.LogFormat("[BooleanMaze #{2}] Ending Location:({0},{1})", correctgridrow, correctgridcol, _moduleId);
        Debug.LogFormat("[BooleanMaze #{1}] Display is {0}", booldisplay, _moduleId);

        //Handles button presses
        ButtonUp.OnInteract += delegate () { HandlePress("u"); return false; };
        ButtonLeft.OnInteract += delegate () { HandlePress("l"); return false; };
        ButtonRight.OnInteract += delegate () { HandlePress("r"); return false; };
        ButtonDown.OnInteract += delegate () { HandlePress("d"); return false; };
        ButtonStuck.OnInteract += delegate () { HandlePress("stuck"); return false; };
        ButtonReset.OnInteract += delegate () { HandlePress("reset"); return false; };
    }

    //Move ending off of AND and NOR
    private void CheckBadEnding()
    {
        while(grid[correctgridrow,correctgridcol] % 3 == 0)
        {
            switch (booldisplay)
            {
                case 0:
                    {
                        correctgridrow--;
                    }
                    break;
                case 1:
                    {
                        correctgridcol++;
                    }
                    break;
                case 2:
                    {
                        correctgridrow++;
                    }
                    break;
                case 3:
                    {
                        correctgridcol--;
                    }
                    break;
            }
            if(correctgridcol > 9 || correctgridcol < 0)
            {
                correctgridcol += (correctgridcol < 0) ? 10 : -10;
            }
            if (correctgridrow > 9 || correctgridrow < 0)
            {
                correctgridrow += (correctgridrow < 0) ? 10 : -10;
            }
        }
    }

    //Returns the position from 0-9 of the letters and numbers of the serial. This is a hacky way of doing it but it works. 
    private int ConvToPos(char serialelement)
    {
        int num = serialelement - '0';
        if(num > 9)
        {
            num += '0' - 'A' + 1;
        }
        while(num > 9)
        {
            num -= 10;
        }
        return num;
    }

    ///Handles button presses using cases based on the label of each button
    ///Each U,L,D,R button checks to see if the defuser is on the edge of the grid, 
    ///and tries to apply a movement in the direction of the button. If the defuser is on
    ///the edge of the grid, or the defuser attempts to enter an illegal grid position, 
    ///a strike is applied. The user is only reset to their initial position upon illegal
    ///use of the Stuck? button.
    private bool HandlePress(string but)
    {
        if(!_isSolved)
        {
            switch (but)
            {
                case "u":
                    {
                        if (gridPosRow == 0)
                        {                           
                            BombModule.HandleStrike();
                            Debug.LogFormat("[BooleanMaze #{2}] Attempted to leave the grid, strike, current position ({0},{1})", gridPosRow, gridPosCol, _moduleId);
                            break;
                        }
                        else if(CheckLegalMove(grid[gridPosRow - 1,gridPosCol]))
                        {                           
                            gridPosRow--;
                            Debug.LogFormat("[BooleanMaze #{2}] Successfully moved up to ({0},{1})", gridPosRow, gridPosCol, _moduleId);
                        }
                        else
                        {
                            BombModule.HandleStrike();
                            Debug.LogFormat("[BooleanMaze #{2}] Attempted to move up to ({0},{1}) with display {6} but the {5} gate returned 0, strike, Current position ({3},{4})", gridPosRow-1, gridPosCol, _moduleId, gridPosRow, gridPosCol, GateCheck(grid[gridPosRow - 1, gridPosCol]), booldisplay);
                        }
                        ButtonUp.AddInteractionPunch();
                    }
                    break;
                case "l":
                    {
                        if (gridPosCol == 0)
                        {                           
                            BombModule.HandleStrike();
                            Debug.LogFormat("[BooleanMaze #{2}] Attempted to leave the grid, strike, current position ({0},{1})", gridPosRow, gridPosCol, _moduleId);
                            break;
                        }
                        else if (CheckLegalMove(grid[gridPosRow, gridPosCol - 1]))
                        {
                            gridPosCol--;
                            Debug.LogFormat("[BooleanMaze #{2}] Successfully moved left to ({0},{1})", gridPosRow, gridPosCol, _moduleId);
                        }
                        else
                        {
                            BombModule.HandleStrike();
                            Debug.LogFormat("[BooleanMaze #{2}] Attempted to move left to ({0},{1}) with display {6} but the {5} gate returned 0, strike, Current position ({3},{4})", gridPosRow, gridPosCol-1, _moduleId, gridPosRow, gridPosCol, GateCheck(grid[gridPosRow, gridPosCol - 1]), booldisplay);
                        }
                        ButtonLeft.AddInteractionPunch();
                    }
                    break;
                case "r":
                    {
                        if (gridPosCol == 9)
                        {
                            BombModule.HandleStrike();
                            Debug.LogFormat("[BooleanMaze #{2}] Attempted to leave the grid, strike, current position ({0},{1})", gridPosRow, gridPosCol, _moduleId);                           
                            break;
                        }
                        else if (CheckLegalMove(grid[gridPosRow, gridPosCol + 1]))
                        {
                            gridPosCol++;
                            Debug.LogFormat("[BooleanMaze #{2}] Successfully moved right to ({0},{1})", gridPosRow, gridPosCol, _moduleId);
                        }
                        else
                        {
                            BombModule.HandleStrike();
                            Debug.LogFormat("[BooleanMaze #{2}] Attempted to move right to ({0},{1}) with display {6} but the {5} gate returned 0, strike, Current position ({3},{4})", gridPosRow, gridPosCol+1, _moduleId, gridPosRow, gridPosCol, GateCheck(grid[gridPosRow, gridPosCol + 1]), booldisplay);
                        }
                        ButtonRight.AddInteractionPunch();
                    }
                    break;
                case "d":
                    {
                        if (gridPosRow == 9)
                        {
                            BombModule.HandleStrike();
                            Debug.LogFormat("[BooleanMaze #{2}] Attempted to leave the grid, strike, current position ({0},{1})", gridPosRow, gridPosCol, _moduleId);                          
                            break;
                        }
                        else if (CheckLegalMove(grid[gridPosRow + 1, gridPosCol]))
                        {
                            gridPosRow++;
                            Debug.LogFormat("[BooleanMaze #{2}] Successfully moved down to ({0},{1})", gridPosRow, gridPosCol, _moduleId);
                        }
                        else
                        {
                            BombModule.HandleStrike();
                            Debug.LogFormat("[BooleanMaze #{2}] Attempted to move down to ({0},{1}) with display {6} but the {5} gate returned 0, strike, Current position ({3},{4})", gridPosRow+1, gridPosCol, _moduleId, gridPosRow, gridPosCol, GateCheck(grid[gridPosRow + 1, gridPosCol]), booldisplay);
                        }
                        ButtonDown.AddInteractionPunch();
                    }
                    break;
                case "stuck":
                    {
                        if (!isStuck())
                        {                           
                            BombModule.HandleStrike();
                            Debug.LogFormat("[BooleanMaze #{2}] Defuser pressed Stuck? at ({0},{1}) but there was a legal move, strike, position reset to ({3},{4}).", gridPosRow, gridPosCol, _moduleId, initrow, initcol);

                            //Reset position to initial starting location! 
                            gridPosRow = initrow;
                            gridPosCol = initcol;
                        }
                        else
                        {
                            Debug.LogFormat("[BooleanMaze #{2}] Defuser correctly pressed Stuck? at ({0},{1}) with no legal moves. Display changed.", gridPosRow, gridPosCol, _moduleId);
                            stuck = true; 
                        }
                        ButtonStuck.AddInteractionPunch();
                    }
                    break;
                case "reset":
                    {
                        gridPosRow = initrow;
                        gridPosCol = initcol;
                        Debug.LogFormat("[BooleanMaze #{2}] Defuser pressed Reset! Position reset to ({0},{1}).", initrow, initcol, _moduleId);
                        ButtonReset.AddInteractionPunch();
                    }
                    break;
            }

            KMAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, this.transform);

            //Check if the Defuser has reached the goal
            CheckPass();

            //Update Display
            StartCoroutine(UpdateDisplay());

        }

        return false;
    }

    /// Updates the number on the display
    private IEnumerator UpdateDisplay()
    {
        NumDisplay.text = "";
        yield return new WaitForSeconds(0.2f);
        int prevbool = booldisplay;
        if (stuck)
        {
            while (booldisplay == prevbool)
            {
                booldisplay = Random.Range(0, 4);
            }
            stuck = false;
        }
        else
        {
            booldisplay = Random.Range(0, 4);
        }

        NumDisplay.text = booldisplay + "";
        Debug.LogFormat("[BooleanMaze #{1}] Display updated to {0}", booldisplay, _moduleId);
    }

    /// This function is used to determine if the defuser is completely stuck when pressing the Stuck? button
    /// If this function returns true the user has no legal move and the screen can be updated, otherwise a strike is applied 
    /// and the defuser is reset to their initial position
    private bool isStuck()
    {
        bool leftcheck = false;
        bool rightcheck = false;
        bool upcheck = false;
        bool downcheck = false;
    
        if(gridPosRow < 9) { downcheck = !CheckLegalMove(grid[gridPosRow + 1, gridPosCol]); }
        else { downcheck = true; }

        if (gridPosRow > 0) { upcheck = !CheckLegalMove(grid[gridPosRow - 1, gridPosCol]); }
        else { upcheck = true; }

        if (gridPosCol < 9) { rightcheck = !CheckLegalMove(grid[gridPosRow, gridPosCol + 1]); }
        else { rightcheck = true; }

        if (gridPosCol > 0) { leftcheck = !CheckLegalMove(grid[gridPosRow, gridPosCol - 1]); }
        else { leftcheck = true; }

        return downcheck && upcheck && leftcheck && rightcheck; //Only true if all legal move checks return false, or you are on the edge
    }

    /// Checks to see if the move the user is trying to apply is legal, based on the logic gate in the square of the grid they attempted to enter
    /// Returns true if the move is legal
    private bool CheckLegalMove(int oper)
    {
        bool legal = false;
        
        switch (oper)
        {
            case 0:
                {
                    if (booldisplay == 0) legal = true;
                }
                break; 
            case 1:
                {
                    if (booldisplay == 1 || booldisplay == 2) legal = true;
                }
                break;
            case 2:
                {
                    if (booldisplay == 1 || booldisplay == 2 || booldisplay == 3) legal = true;
                }
                break;
            case 3:
                {
                    if (booldisplay == 3) legal = true;
                }
                break;
        }

        if (legal) return true;

        return false;
    }

    /// Checks to see if the defuser is at the goal or not
    private bool CheckPass()
    {
        if (gridPosRow == correctgridrow && gridPosCol == correctgridcol)
        {
            Debug.LogFormat("[BooleanMaze #{0}] Defuser reached the goal. Module solved.", _moduleId);
            BombModule.HandlePass();          
            _isSolved = true;
        }
        return false; 
    }

    //Used for debug log
    private string GateCheck(int gateId)
    {
        string gateName = "Gate Not Found";
        switch (gateId)
        {
            case 0:
                {
                    gateName = "NOR";
                }
                break;
            case 1:    
                {
                    gateName = "XOR";
                }
                break;
            case 2:
                {
                    gateName = "OR";
                }
                break;
            case 3:
                {
                    gateName = "AND";
                }
                break;
        }
        return gateName;
    }
}
