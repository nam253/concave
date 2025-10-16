using UnityEngine;
using UnityEngine.UI;

public class Game : MonoBehaviour
{
    enum State
    {
        Start = 0,
        Game,
        End,
    };

    enum Turn
    {
        I = 0,
        You,
    }
    enum Stone
    {
        None = 0,
        White,
        Black,
    }

    Tcp tcp;
    public InputField ip;
    public Texture texBoard;
    public Texture texWhite;
    public Texture texBlack;

    int[] board = new int[9];
    State state;
    Stone stoneTurn;
    Stone stoneI;
    Stone stoneYou;
    Stone stoneWinner;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        tcp = GetComponent<Tcp>();
        state = State.Start;
        for (int i = 0; i < board.Length; ++i)
        {
            board[i] = (int)Stone.None;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (!tcp.IsConnect()) return;
        if (state == State.Start)
        {
            UpdateStart();
        }
        if (state == State.Game)
        {
            UpdateGame();
        }
        if (state == State.End)
        {
            UpdateEnd();
        }
    }

    void UpdateStart()
    {
        state = State.Game;
        stoneTurn = Stone.White;

        if (tcp.IsServer())
        {
            stoneI = Stone.White;
            stoneYou = Stone.Black;
        }
        else
        {
            stoneI = Stone.Black;
            stoneYou = Stone.White;
        }
    }

    void UpdateGame()
    {
        bool bSet = false;
        if (stoneTurn == stoneI)
        {
            bSet = MyTurn();
        }

        else
        {
            bSet = YourTurn();
        }

        if (bSet == false)
        {
            return;
        }

        stoneWinner = CheckBoard();
        if (stoneWinner != Stone.None)
        {
            state = State.End;
            Debug.Log("½Â¸®: " + (int)stoneWinner);
        }
        stoneTurn = (stoneTurn == Stone.White) ? Stone.Black : Stone.White;
    }

    bool YourTurn()
    {
        byte[] data = new byte[1];
        int iSize = tcp.Receive(ref data, data.Length);
        if (iSize <= 0)
        {
            return false;
        }

        int i = (int)data[0];
        Debug.Log("¹ÞÀ½:" + i);

        bool ret = SetStone(i, stoneYou);
        if (ret == false)
        {
            return false;
        }

        return true;
    }

    Stone CheckBoard()
    {
        for (int i = 0; i < 2; i++)
        {
            int s;
            if (i == 0)
                s = (int)Stone.White;
            else
                s = (int)Stone.Black;

            if (s == board[0] && s == board[1] && s == board[2])
                return (Stone)s;
            if (s == board[3] && s == board[4] && s == board[5])
                return (Stone)s;
            if (s == board[6] && s == board[7] && s == board[8])
                return (Stone)s;

            if (s == board[0] && s == board[3] && s == board[6])
                return (Stone)s;
            if (s == board[1] && s == board[4] && s == board[7])
                return (Stone)s;
            if (s == board[2] && s == board[5] && s == board[8])
                return (Stone)s;

            if (s == board[0] && s == board[4] && s == board[8])
                return (Stone)s;
            if (s == board[2] && s == board[4] && s == board[6])
                return (Stone)s;
        }

        return Stone.None;
    }





    void UpdateEnd()
    {

    }

    bool SetStone(int i, Stone stone)
    {
        if (board[i] == (int)Stone.None)
        {
            board[i] = (int)stone;
            return true;
        }
        return false;
    }

    int PosToNumber(Vector3 pos)
    {
        float x = pos.x - 50;
        float y = Screen.height - 50 - pos.y;
        if (x < 0.0f || x >= 300.0f)
        {
            return -1;
        }
        if (y < 0.0f || y >= 300.0f)
        {
            return -1;
        }

        int h = (int)(x / 100.0f);
        int v = (int)(y / 100.0f);
        int i = v * 3 + h;
        return i;
    }

    bool MyTurn()
    {
        bool bClick = Input.GetMouseButtonDown(0);
        if (!bClick)
        {
            return false;
        }
        Vector3 pos = Input.mousePosition;
        int i = PosToNumber(pos);
        if (i == -1)
        {
            return false;
        }

        bool bSet = SetStone(i, stoneI);
        if (bSet == false)
        {
            return false;
        }
        byte[] data = new byte[1];
        data[0] = (byte)i;
        tcp.Send(data, data.Length);
        Debug.Log("º¸³¿:" + i);
        return true;
    }




    public void ServerStart()
    {
        tcp.StartServer(10000, 10);
    }
    public void ClientStart()
    {
        tcp.Connect(ip.text, 10000);
    }

    void OnGUI()
    {
        if (!Event.current.type.Equals(EventType.Repaint))
            return;

        Graphics.DrawTexture(new Rect(0, 0, 400, 400), texBoard);

        for (int i = 0; i < board.Length; ++i)
        {
            if (board[i] != (int)Stone.None)
            {
                float x = 50 + (i % 3) * 100;
                float y = 50 + (i / 3) * 100;
                Texture tex = (board[i] == (int)Stone.White) ?
                texWhite : texBlack;
                Graphics.DrawTexture(new Rect(x, y, 100, 100),
                tex);
            }
        }

        if (state == State.Game)
        {
            if (stoneTurn == Stone.White)
                Graphics.DrawTexture(new Rect(0, 400, 100, 100),
                texWhite);
            else
                Graphics.DrawTexture(new Rect(300, 400, 100, 100), texBlack);
        }

        if (state == State.End)
        {
            if (stoneWinner == Stone.White)
                Graphics.DrawTexture(new Rect(150, 400, 100, 100), texWhite);
            else
                Graphics.DrawTexture(new Rect(150, 400, 100, 100), texBlack);
        }
    }

}

