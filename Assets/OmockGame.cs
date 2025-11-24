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
    Stone[,] board = new Stone[BOARD_SIZE, BOARD_SIZE]; //판에 어떠한 돌이 놓여 있는지 저장, 15x15 크기의 2차원 배열

    State state; //현재 상태를 저장

    Stone stoneTurn; //턴 소유자의 돌 색을 저장
    Stone stoneI; // 나의 돌 색 저장
    Stone stoneYou; // 상대방의 돌 색 저장
    Stone stoneWinner; //종료 후 승리한 돌의 색 저장


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

        if (tcp.IsServer()) //내가 서버 역할을 한다면 흰돌, 상대방은 검은 돌
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

        if (stoneTurn == stoneI) //현재 턴의 돌 색이 나의 돌 색과 같다면 내가 움직임
            bSet = MyTurn();
        else
            bSet = YourTurn(); // 내턴이 아니라면 상태방을 기다림

        if (!bSet) //돌을 놓지 못했거나 네트워크 데이터가 없다면 턴을 넘기지 않고 대기
            return;

        if (state != State.End) //게임이 끝나지 않았고 돌 놓기 성공했다면 현재턴을 다음 색깔로 바꿈
            stoneTurn = (stoneTurn == Stone.White) ? Stone.Black : Stone.White;
    }

    bool YourTurn()
    {
        byte[] data = new byte[1024];
        int iSize = tcp.Receive(ref data, data.Length); //네트워크 데이터를 수신
        if (iSize <= 0)
            return false; //데이터가 없으면 false반환

        int idx = 0;
        while (idx < iSize) //여러개의 메세지가 포함될 수 있으므로 반복해서 처리
        {
            byte msgType = data[idx]; //메세지 첫 번째 바이트는 메시지 유형(0:놓기, 1제거)

            if (idx + 2 >= iSize)
            {
                Debug.LogWarning("Incomplete message received");
                break;
            }

            byte x = data[idx + 1]; //두번째 바이트는 돌이 놓인 좌표
            byte y = data[idx + 2]; //세번째 바이트는 돌이 제거된 좌표

            idx += 3;

            if (msgType == 0) // 상대방 돌 놓기 메시지 보드에 반영, 상대방이 오목을 만들었는지 확인하여 승리를 처리
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
                    board[x, y] = Stone.None; //해당 좌표의 돌을 none으로 바꿔 제거
                }
            }
            else
            {
                Debug.LogWarning("Unknown message type: " + msgType);
            }
        }

        return true; //데이터 수신 및 처리에 성공했으므로 턴을 넘김
    }

    bool SetStone(int x, int y, Stone stone) //특정 좌표에 돌을 놓는다, 유효범위 내이고 칸이 비어 있을 때만 성공
    {
        if (!InBoard(x, y)) return false;
        if (board[x, y] != Stone.None) return false;

        board[x, y] = stone;

        return true;
    }

    bool SetStone(int index, Stone stone) //돌을 놓는 작업이 성공했는지 실패했는지를 반환
    {
        int x = index % BOARD_SIZE; //열의 좌표를 x좌표로 변환
        int y = index / BOARD_SIZE; //행의 좌표를 y좌표로 변환
        return SetStone(x, y, stone); // 핵심 함수에게 제어권을 넘겨주고 결과를 그대로 돌려주는 역할
    }
    bool MyTurn()
    {
        if (!Input.GetMouseButtonDown(0)) //마우스 왼쪽 클릭을 하지 않았다면 false를 반환
            return false;

        Vector3 pos = Input.mousePosition;
        int x, y;
        if (!PosToXY(pos, out x, out y)) //마우스 좌표를 보드 좌표x,y로 변환하고 유효 범위 밖이라면 false를 반환
            return false;

        bool ok = SetStone(x, y, stoneI); // 계산된 x,y에 내 돌을 놓고 이미 돌이 있다면 false
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


        SendPlace(x, y); //내가 돌을 놓은 위치를 상대방에게 전송


        foreach (var p in removed) // 포획하여 제거한 돌의 위치를 상대방에게 전송
        {
            SendRemove(p.x, p.y);
        }

        return true; //돌 놓기와 통신에 성공하면 턴을 넘김
    }

    struct Point { public int x, y; public Point(int a, int b) { x = a; y = b; } } //x좌표와 y좌표를 묶어서 하나의 좌표로 나타내기 위해 만든 구조체

    System.Collections.Generic.List<Point> CaptureStones(int x, int y, Stone me)//이 함수는 제거된 돌들의 좌표 목록을 담는 리스트 (List<Point>)를 반환합니다.
    {
        System.Collections.Generic.List<Point> removed = new System.Collections.Generic.List<Point>(); //나-상대-나 패턴이 있는지 검사하고 이 패턴이 있다면 가운데 돌을 제거하여 좌표를 리스트로 반환

        int[] dx = { 1, 0, 1, 1 }; //오목의 4가지 기본 방향(가로, 세로, 오른쪽 아래 대각선, 오른쪽 위 대각선)의 x및 y 벡터 정의
        int[] dy = { 0, 1, 1, -1 };

        Stone opponent = (me == Stone.White) ? Stone.Black : Stone.White; //내가 흰돌이면 상대는 검은 돌, 내가 검은 돌이면 상대는 흰돌로 상대방 색을 결정

        for (int dir = 0; dir < 4; dir++) //4가지 방향 각각에 대해 포획이 발생했는지 검사
        {
            int mx = x + dx[dir]; //해당 방향으로 1칸 떨어진 위치의 좌표를 계산(여기에 상대방 돌이 있어야함)
            int my = y + dy[dir];
            int ex = x + dx[dir] * 2; //해당 방향으로 2칸 떨어진 위치의 좌표를 계산(여기에 나의 돌이 있어야 포획이 성립)
            int ey = y + dy[dir] * 2;

            if (!InBoard(mx, my) || !InBoard(ex, ey)) continue; //계산된 중간 위치나 끝 위치 중 하나라도 오목판 범위 밖이라면 포획이 불가능, 방향 검사를 건너뛰고 다음 방향으로

            if (board[ex, ey] == me && board[mx, my] == opponent) //2칸 떨어진 위치에 나의 돌이 있고 한칸 떨어진 곳에 상대방 돌이 있다면 나-상대-나 가 완성
            {

                board[mx, my] = Stone.None; //포획이 확인되었으므로, 가운데 있는 돌의 위치를 돌 없음으로 설정하여 제거
                removed.Add(new Point(mx, my)); //제거된 돌의 좌표를 removes 리스트에 추가
            }
        }

        return removed; //4가지 방향에 대한 검사를 모두 완료한 후, 포획되어 제거된 모든 돌들의 좌표가 담긴 리스트를 반환합니다.
    }
    bool CheckFive(int x, int y, Stone me) //5개 연속된 돌이 있는지 검사하여 반환하는 함수
    {
        int[] dx = { 1, 0, 1, 1 }; //(1,0)가로, (0,1)세로
        int[] dy = { 0, 1, 1, -1 }; //(1,1)대각선 오른쪽 아래, (1,-1)대각선 오른쪽 위

        for (int dir = 0; dir < 4; dir++) //4가지 주요 방향(가로, 세로, 두 대각선) 각각에 대해 검사를 반복
        {
            int count = 1; //연속된 돌의 개수를 저장하는 변수, 1로 초기화 하는 이유는 방금 놓은 돌 자신을 이미 1개로 계산하기 때문
            count += CountDirection(x, y, dx[dir], dy[dir], me); //CountDirection 함수를 호출하여, 현재 방향(dx[dir], dy[dir])으로 연속된 같은 색 돌이 몇 개인지 세고 그 개수를 count에 더합니다.
            count += CountDirection(x, y, -dx[dir], -dy[dir], me); //같은 방향 벡터에 -를 붙여 정반대 방향으로 연속된 돌의 개수를 셉니다. (예: 왼쪽 방향)

            if (count >= 5) return true; //합이 5게 이상이라면, 5목을 달성 함수를 종료하여 true(승리)를 반환
        }
        return false; //검사해도 5개 이상이 없다면 승리 아님을 반환
    }

    int CountDirection(int x, int y, int dx, int dy, Stone me) //특정 방향으로 연속된 같은 색 돌의 개수를 센다.
    {
        int c = 0; //연속된 돌의 개수를 저장할 변수
        int nx = x + dx; //방향 벡터 dx, dy 만큼 이동하여 바로 다음 칸의 좌표를 계산
        int ny = y + dy;
        while (InBoard(nx, ny) && board[nx, ny] == me) //두가지 조건을 만족하는 동안 반복, 경계 내에 있는가, 내가 놓은 돌의 색과 같은 색의 돌이 있는가
        {
            c++; //모두 참이라면 증가
            nx += dx; ny += dy; //다음 검사를 위해 좌표를 현재 방향으로 한칸 더 이동

            //하나라도 조건이 거짓이라면 루프를 종료
        }
        return c;//루프 종료시 현재 방향으로 기준 돌을 제외하고 연속되어 있던 돌의 총 개수 c를 checkfive 함수로 반환
    }

    bool InBoard(int x, int y) // 주어진 x, y 좌표가 오목판의 범위 내에 있는지 확인
    {
        return x >= 0 && x < BOARD_SIZE && y >= 0 && y < BOARD_SIZE;
    }

    bool PosToXY(Vector3 pos, out int x, out int y) // 마우스 클릭 좌표를 보드의 배열 인덱스로 변환
    {
        x = -1; y = -1; //일단 -1로 초기화

        int px = boardMargin; //오목판이 시작되는 위치의 픽셀 좌표를 변수에 저장 boardMargin는 20픽셀
        int py = boardMargin;
        int size = boardPixelSize;//오목판의 전체 픽셀 크기를 저장 600픽셀

        if (pos.x < px || pos.x >= px + size) return false; //영역을 벗어나면 false를 반환
        float invY = Screen.height - pos.y;
        if (invY < py || invY >= py + size) return false;

        float cell = (float)size / BOARD_SIZE; //오목판 전체 픽셀을 보드 크기로 나누어 픽셀 크기를 계산

        x = (int)((pos.x - px) / cell); // x좌표에서 오목판 시작 여백을 빼서 내부의 상대적인 x위치를 구함
                                        // cell : 이 상대 위치를 한 칸의 픽셀 크기로 나누면, 몇 번째 칸에 해당하는지 소수점이 나옵니다.
        y = (int)((invY - py) / cell); // 변환된 $Y$ 좌표(invY)에 대해서도 동일한 계산을 수행하여 최종 인덱스를 얻습니다.

        if (!InBoard(x, y)) return false; //유효 범위를 벗어났는지 한 번 더 검사
        return true; //검사를 통과하고 올바르게 설정되었다면 성공적으로 변환
    }
    void SendPlace(int x, int y) //상대방에게 돌 놓기 정보를 전송
    {
        byte[] data = new byte[3];
        data[0] = 0; // 메시지 유형(0 또는 1) 0은 돌을 놓았다
        data[1] = (byte)x; //x좌표
        data[2] = (byte)y; //y좌표
        tcp.Send(data, data.Length); //tcp 컴포넌트의 send 함수를 호출하여 준비된 3바이트 배열을 상대방에게 전송
        Debug.Log("보냄 place: " + x + "," + y);
    }

    void SendRemove(int x, int y) //상대방에게 돌 제거 정보를 전송
    {
        byte[] data = new byte[3];
        data[0] = 1; // 1은 돌을 제거했다는 메세지
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
        if (!Event.current.type.Equals(EventType.Repaint)) //호출된 이벤트의 유형을 확인하고 화면을 다시 그려야 할 때 발생하는 이벤트
            return;

        // 텍스터 이미지를 그리는 함수, 시작 위치, 크기, 배경 이미지
        Graphics.DrawTexture(new Rect(boardMargin, boardMargin, boardPixelSize, boardPixelSize), textBord);

        float cell = (float)boardPixelSize / BOARD_SIZE; // 오목판의 전체 픽셀 크기를 오목판 격자 크기로 나우어 오목판 한 칸의 픽셀 크기를 계산

        for (int y = 0; y < BOARD_SIZE; ++y) // for 루프를 사용해 오목판의 모든 칸을 순회
        {
            for (int x = 0; x < BOARD_SIZE; ++x)
            {
                if (board[x, y] != Stone.None) //현재 좌표에 돌이 놓여 있는지 확인, 돌이 있는 경우 다음 코드를 실행
                {
                    float px = boardMargin + x * cell; //돌을 그려야 할 화면 x좌표를 계산
                    float py = boardMargin + y * cell; //돌을 그려야 할 화면 y 좌표를 계산
                    Texture tex = (board[x, y] == Stone.White) ? textWhite : textBlack; // 현재 칸의 돌 색을 확인
                    Graphics.DrawTexture(new Rect(px, py, cell, cell), tex); // 계산된 위치와 셀 크기에 해당 색의 돌 텍스처를 그려 넣음
                }
            }
        }

        // 현재 턴 표시
        if (state == State.Game)
        {
            if (stoneTurn == Stone.White)
                Graphics.DrawTexture(new Rect(0, boardPixelSize + boardMargin + 10, 60, 60), textWhite);
            else
                Graphics.DrawTexture(new Rect(boardPixelSize + boardMargin - 60, boardPixelSize + boardMargin + 10, 60, 60), textBlack);
        }

        // 승자 표시
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
