using UnityEngine;
using UnityEngine.UI;

public class OmockGame : MonoBehaviour
{
    enum State { Start = 0, Game, End }; //게임의 상태(준비, 진행중, 종료)
    enum Turn { I = 0, You } // 턴 소유자 (나 또는 상대)
    enum Stone { None = 0, White, Black } // 돌의 상태(돌 없음, 흰 돌, 검은 돌)

    Tcp tcp;
    public InputField ip;
    public Texture textBord;
    public Texture textWhite;
    public Texture textBlack;

    // board size
    const int BOARD_SIZE = 15; // 오목 보드판의 크기를 15x15로 정의

    // 2D board
    Stone[,] board = new Stone[BOARD_SIZE, BOARD_SIZE]; //돌의 상태를 저장, 15x15 크기의 2차원 배열

    State state;

    Stone stoneTurn;
    Stone stoneI;
    Stone stoneYou;
    Stone stoneWinner;


    int boardPixelSize = 600; //보드판의 픽셀 크기
    int boardMargin = 20; // 보드 판이 좌측 상단에서 떨어져 있는 여백

    void Start()
    {
        tcp = GetComponent<Tcp>(); //tcp 컴포넌트를 가져오기
        state = State.Start; // 게임 상태를 start로 초기화

        for (int y = 0; y < BOARD_SIZE; ++y) // 모든 칸을 돌 없음으로 초기화
            for (int x = 0; x < BOARD_SIZE; ++x)
                board[x, y] = Stone.None;
    }

    void Update()
    {
        if (!tcp.IsConnect()) return; //연결되어 있지 않다면 상태 종료

        if (state == State.Start) 
            UpdateStart();
        if (state == State.Game)
            UpdateGame();
        if (state == State.End)
            UpdateEnd();
    }

    void UpdateStart()
    {
        state = State.Game;
        stoneTurn = Stone.White; // 흰돌이 첫 턴을 가지도록 설정

        if (tcp.IsServer()) //역할에 따라 돌의 색을 결정
        {
            stoneI = Stone.White; // 나의 턴이면 흰 돌
            stoneYou = Stone.Black; // 상대방이라면 검은 돌
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

        if (stoneTurn == stoneI) //현재 턴이 나라면 myturn 함수를 호출
            bSet = MyTurn();
        else
            bSet = YourTurn(); // 상대방 턴이라면 yourturn 함수 호출

        if (!bSet)
            return;

        if (state != State.End) //게임이 끝나지 않았고 돌 놓기 성공했다면 현재턴을 다음 색깔로 바꿈
            stoneTurn = (stoneTurn == Stone.White) ? Stone.Black : Stone.White;
    }

    bool YourTurn()
    {
        byte[] data = new byte[1024];
        int iSize = tcp.Receive(ref data, data.Length); //네트워크 데이터를 수신
        if (iSize <= 0)
            return false;

        int idx = 0;
        while (idx < iSize) //여러개의 메세지가 포함될 수 있으므로 루프 처리
        {
            byte msgType = data[idx]; //3바이트 메세지 구조 유형, x좌표, y좌표에 따라 데이터 해석
            
            if (idx + 2 >= iSize)
            {
                Debug.LogWarning("Incomplete message received");
                break;
            }

            byte x = data[idx + 1];
            byte y = data[idx + 2];

            idx += 3;

            if (msgType == 0) // 상대방 돌 놓기 메시지 보드에 반영
            {
                bool ok = SetStone((int)x, (int)y, stoneYou);
                if (!ok)
                {
                    Debug.LogWarning("Opponent placed on invalid or occupied cell: " + x + "," + y);
                }
   
                if (CheckFive((int)x, (int)y, stoneYou)) //상대방의 수가 5목을 만들었다면 상대방 승리 처리 후 게임 종료
                {
                    state = State.End;
                    stoneWinner = stoneYou;
                    Debug.Log("상대 승리: " + stoneWinner);
                }
            }
            else if (msgType == 1) // 상대방이 돌을 포획하여 제거했다는 메시지
            {
                
                if (InBoard((int)x, (int)y))
                {
                    board[x, y] = Stone.None;
                }
            }
            else
            {
                Debug.LogWarning("Unknown message type: " + msgType);
            }
        }

        return true;
    }

    bool SetStone(int x, int y, Stone stone) //특정 좌표에 돌을 놓는다, 유효범위 내이고 칸이 비어 있을 때만 성공
    {
        if (!InBoard(x, y)) return false;
        if (board[x, y] != Stone.None) return false;

        board[x, y] = stone;

        return true;
    }

    bool SetStone(int index, Stone stone) // compatibility (not used anymore)
    {
        int x = index % BOARD_SIZE;
        int y = index / BOARD_SIZE;
        return SetStone(x, y, stone);
    }

    bool MyTurn()
    {
        if (!Input.GetMouseButtonDown(0)) //왼쪽 클릭이 없다면 false를 반환
            return false;

        Vector3 pos = Input.mousePosition;
        int x, y;
        if (!PosToXY(pos, out x, out y)) //마우스 좌표를 보드 좌표x,y로 변환하고 유효 범위 밖이라면 false를 반환
            return false;

        bool ok = SetStone(x, y, stoneI); // 유효한 위치에 돌을 놓는다
        if (!ok) return false;

      
        if (CheckFive(x, y, stoneI))//돌을 놓은 후 5개 이상 연결을 만들었는지 확인
        {
            state = State.End; // 오목을 만들었다면 승리 처리 하고 게임 동료
            stoneWinner = stoneI;

    
            SendPlace(x, y); // 상대방에게 돌 놓기 정보를 전송

            Debug.Log("승리: " + stoneWinner);
            return true;
        }

  
        var removed = CaptureStones(x, y, stoneI); // 포획 규칙을 검사하여 제거된 돌의 목록을 얻는다.


        SendPlace(x, y);

  
        foreach (var p in removed) // 포획된 돌이 있다념 상대방에게 돌 제거 정보를 전송
        {
            SendRemove(p.x, p.y);
        }

        return true;
    }

    struct Point { public int x, y; public Point(int a, int b) { x = a; y = b; } }

    // CaptureStones: returns list of removed points so we can sync over network
    System.Collections.Generic.List<Point> CaptureStones(int x, int y, Stone me)
    {
        System.Collections.Generic.List<Point> removed = new System.Collections.Generic.List<Point>(); //나-상대-나 패턴이 있는지 검사하고 이 패턴이 있다면 가운데 돌을 제거하여 좌표를 리스트로 반환

        int[] dx = { 1, 0, 1, 1 };
        int[] dy = { 0, 1, 1, -1 };

        Stone opponent = (me == Stone.White) ? Stone.Black : Stone.White;

        for (int dir = 0; dir < 4; dir++)
        {
            int mx = x + dx[dir];
            int my = y + dy[dir];
            int ex = x + dx[dir] * 2;
            int ey = y + dy[dir] * 2;

            if (!InBoard(mx, my) || !InBoard(ex, ey)) continue;

            if (board[ex, ey] == me && board[mx, my] == opponent)
            {
                // remove middle stone
                board[mx, my] = Stone.None;
                removed.Add(new Point(mx, my));
            }
        }

        return removed;
    }

    bool CheckFive(int x, int y, Stone me) //5개 연속된 돌이 있는지 검사
    {
        int[] dx = { 1, 0, 1, 1 };
        int[] dy = { 0, 1, 1, -1 };

        for (int dir = 0; dir < 4; dir++)
        {
            int count = 1;
            count += CountDirection(x, y, dx[dir], dy[dir], me);
            count += CountDirection(x, y, -dx[dir], -dy[dir], me);

            if (count >= 5) return true;
        }
        return false;
    }

    int CountDirection(int x, int y, int dx, int dy, Stone me) //특정 방향으로 연속된 같은 색 돌의 개수를 센다.
    {
        int c = 0;
        int nx = x + dx;
        int ny = y + dy;
        while (InBoard(nx, ny) && board[nx, ny] == me)
        {
            c++;
            nx += dx; ny += dy;
        }
        return c;
    }

    bool InBoard(int x, int y) //좌표가 보드판 범위 내에 있는지 확인
    {
        return x >= 0 && x < BOARD_SIZE && y >= 0 && y < BOARD_SIZE;
    }

    bool PosToXY(Vector3 pos, out int x, out int y) // 마우스 클릭 좌표를 보드의 배열 인덱스로 변환
    {
        x = -1; y = -1;

        int px = boardMargin;
        int py = boardMargin;
        int size = boardPixelSize;

        if (pos.x < px || pos.x >= px + size) return false;
        float invY = Screen.height - pos.y;
        if (invY < py || invY >= py + size) return false;

        float cell = (float)size / BOARD_SIZE;

        x = (int)((pos.x - px) / cell);
        y = (int)((invY - py) / cell);

        if (!InBoard(x, y)) return false;
        return true;
    }

    void SendPlace(int x, int y) //상대방에게 돌 놓기 정보를 전송
    {
        byte[] data = new byte[3];
        data[0] = 0; // place
        data[1] = (byte)x;
        data[2] = (byte)y;
        tcp.Send(data, data.Length);
        Debug.Log("보냄 place: " + x + "," + y);
    }

    void SendRemove(int x, int y) //상대방에게 돌 제거 정보를 전송
    {
        byte[] data = new byte[3];
        data[0] = 1; // remove
        data[1] = (byte)x;
        data[2] = (byte)y;
        tcp.Send(data, data.Length);
        Debug.Log("보냄 remove: " + x + "," + y);
    }

    void UpdateEnd()
    {
        // game finished, could add restart or UI here
    }

    void OnGUI()
    {
        if (!Event.current.type.Equals(EventType.Repaint))
            return;

        // draw board background
        Graphics.DrawTexture(new Rect(boardMargin, boardMargin, boardPixelSize, boardPixelSize), textBord);

        float cell = (float)boardPixelSize / BOARD_SIZE;

        for (int y = 0; y < BOARD_SIZE; ++y)
        {
            for (int x = 0; x < BOARD_SIZE; ++x)
            {
                if (board[x, y] != Stone.None)
                {
                    float px = boardMargin + x * cell;
                    float py = boardMargin + y * cell;
                    Texture tex = (board[x, y] == Stone.White) ? textWhite : textBlack;
                    Graphics.DrawTexture(new Rect(px, py, cell, cell), tex);
                }
            }
        }

        // turn display
        if (state == State.Game)
        {
            if (stoneTurn == Stone.White)
                Graphics.DrawTexture(new Rect(0, boardPixelSize + boardMargin + 10, 60, 60), textWhite);
            else
                Graphics.DrawTexture(new Rect(boardPixelSize + boardMargin - 60, boardPixelSize + boardMargin + 10, 60, 60), textBlack);
        }

        // winner
        if (state == State.End)
        {
            if (stoneWinner == Stone.White)
                Graphics.DrawTexture(new Rect((boardPixelSize + boardMargin) / 2 - 30, boardPixelSize + boardMargin + 10, 60, 60), textWhite);
            else
                Graphics.DrawTexture(new Rect((boardPixelSize + boardMargin) / 2 - 30, boardPixelSize + boardMargin + 10, 60, 60), textBlack);
        }
    }

    public void ServerStart()
    {
        // 포트와 백로그(동시 접속 허용 수)는 필요에 따라 변경
        int port = 10000;
        int backlog = 10;

        if (tcp == null)
            tcp = GetComponent<Tcp>();

        tcp.StartServer(port, backlog);
        Debug.Log("서버 시작 요청: 포트 " + port);
    }

    public void ClientStart()
    {
        if (tcp == null)
            tcp = GetComponent<Tcp>();

        if (ip == null)
        {
            Debug.LogWarning("IP InputField가 연결되어 있지 않습니다.");
            return;
        }

        string targetIp = ip.text;
        int port = 10000;
        tcp.Connect(targetIp, port);
        Debug.Log("클라이언트 연결 시도: " + targetIp + ":" + port);
    }
}

