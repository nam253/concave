using UnityEngine;
using UnityEngine.UI;

public class SamMok : MonoBehaviour

{
    enum State //enum은 열거형 이름이 있는 상수들의 집합을 정의
    {
        Start = 0, //start는 명시적으로 0을 할당
        Game, //start의 값에 1이 증가된 값인 1이 자동 할당
        End, //Game의 값에 1이 증가된 값인 2가 자동 할당
    };

    enum Turn //턴 소유자를 구분하기 위한 열거형
    {
        I = 0, //턴 소유자가 나인지
        You, //턴 소유자가 상대방인지
    }

    enum Stone //바둑돌 색 구분하기 위한 열거형
    {
        None = 0, //돌 없음
        White, //흰돌
        Black //검은 돌
    }

    Tcp tcp; //통신용 변수 tcp, tp변수 선언
    public InputField ip; //IP주소를 입력 받기 위함
    public Texture textBord; //게임 보드판 이미지
    public Texture textWhite; //흰돌 이미지
    public Texture textBlack; //검은돌 이미지

    //바둑돌 관련 변수
    int[] board = new int[9]; // 9칸의 돌의 상태를 저장(none, white, black)

    State state; //게임 진행 상태 start, game, end

    Stone stoneTurn; //현재 턴을 가진 플레이어의 돌 색깔 저장
    Stone stoneI; //나의 돌의 색깔을 저장
    Stone stoneYou; //상대방의 돌 색깔을 저장
    Stone stoneWinner; //승자의 돌 색깔을 저장

    void Start()
    {
        tcp = GetComponent<Tcp>(); //tcp 타입의 컴포넌트를 찾아 그 인스턴스를 가져와  tcp에 할당

        state = State.Start; //게임 준비 상태

        for (int i = 0; i < board.Length; ++i)
        {
            board[i] = (int)Stone.None; //게임 보드 초기화, 돌이 없는 상태
        }
    }
    void Update()
    {
        if (!tcp.IsConnect()) return; //tcp가 연결되어 있지 않으면 아무 작업 없이 현재 턴을 종료, 네트워크 연결 확인
        if (state == State.Start) //상태가 start이면 updatestart함수 호출하여 게임 시작
        {
            UpdateStart();
        }
        if (state == State.Game) //게임 상태가 game이면 updategame 함수를 호출하여 게임 진행
        {
            UpdateGame();
        }
        if (state == State.End) //게임 상태가 end이면 updateend 함수를 호출하여 종료처리
        {
            UpdateEnd();
        }
    }

    void UpdateStart()
    {
        state = State.Game; //game으로 상태 변경 
        stoneTurn = Stone.White; //흰 돌이 첫턴

        if (tcp.IsServer()) //내가 서버역할이라면
        {
            stoneI = Stone.White; //나는 흰색돌
            stoneYou = Stone.Black; //상대방은 검은 돌
        }
        else //내가 클라이언트라면
        {
            {
                stoneI = Stone.Black; //나는 검정
                stoneYou = Stone.White; //상대방은 흰색
            }
        }
    }

    void UpdateGame()
    {
        bool bSet = false; // 돌을 성공적으로 놓았는지

        if (stoneTurn == stoneI) //나의 턴인지 확인
        {
            bSet = MyTurn(); // 내 턴이라면 MyTurn 함수 호출
        }
        else
        {
            bSet = YourTurn(); //상대방의 네트워크 입력을 받음
        }
        if (bSet == false) //돌 놓기에 실패했다면 턴을 바꾸지 않고 함수를 종료
        {
            return;
        }
        stoneWinner = CheckBoard(); //현재 보드 상태를 확인하여 승자가 있는지 검사

        if (stoneWinner != Stone.None) //승자가 있다면 게임 상태를 end로 변경
        {
            state = State.End;
            Debug.Log("승리: " + (int)stoneWinner); //승자 정보를 콘솔에 출력
        }

        stoneTurn = (stoneTurn == Stone.White) ? Stone.Black : Stone.White; //현재 턴을 흰돌에서 검은돌로, 또는 검은 돌에서 흰돌로 변경
    }
    bool YourTurn() //상대방 턴에 네트워크를 수신하는 함수
    {
        byte[] data = new byte[1];
        int iSize = tcp.Receive(ref data, data.Length); //상대방으로부터 데이터를 수신하여 배열 data에 저장하고, 수신된 바이트 크기를 iSize에 저장

        if (iSize <= 0) //수신된 데이터가 없으면(상대방이 돌을 놓지 않았으면 false를 반환
        {
            return false;
        }
        int i = (int)data[0]; //수신된 1바이트 데이터(상대방이 놓은 위치 인덱스)를 정수로 변환
        Debug.Log("받음: " + i);

        bool ret = SetStone(i, stoneYou); //상대방이 보낸 인덱스 i위치에 상대방의 돌을 놓는다.
        if (ret == false)
        {
            return false;
        }
        return true;
    }
    Stone CheckBoard() //보드 상태를 확인하여 승리 조건을 만족하는 돌이 있는지 검사
    {
        for (int i = 0; i < 2; i++) //흰돌(i =0)과 검은돌(i=1)에 대해 두번 반복
        {
            int s;
            if (i == 0)
                s = (int)Stone.White;
            else
                s = (int)Stone.Black;

            if (s == board[0] && s == board[1] && s == board[2]) //가로 1번째 줄의 돌 색깔이 모두 같은지 확인하여 승자를 반환
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
        return Stone.None; //모든 승자가 없으면 none을 반환
    }

    void UpdateEnd()
    {

    }
    bool SetStone(int i, Stone stone) //보드 배열의 특정 위치에 특정 돌을 놓는 함수
    {
        if (board[i] == (int)Stone.None) //해당 위치에 돌이 없는지 확인
        {
            board[i] = (int)stone; //돌이 없으면 해당 위치에 새 돌을 놓는다
            return true;
        }
        return false; //이미 돌이 있는 경우 false를 반환
    }

    int PosToNumber(Vector3 pos) //마우스 클릭 좌표를 보드 배열 인덱스로 변환
    {
        float x = pos.x - 50; // x좌표에서 오프셋 50을 빼서 보드의 시작점을 0으로 맞춤
        float y = Screen.height - 50 - pos.y; //y좌표는 화면 아래가 0이므로, 위쪽을 기준으로 계산하고 오프셋을 적용

        //유효하지 않은 영역에 클릭 발생시 -1을 반환
        if (x < 0.0f || x >= 300.0f)
        {
            return -1;
        }
        if (y < 0.0f || y >= 300.0f)
        {
            return -1;
        }

        int h = (int)(x / 100.0f); //x좌표를 100으로 나누어 가로 인덱스 (0,1,2)를 계산
        int v = (int)(y / 100.0f); //y좌표를 100으로 나누어 세로 인덱스 (0,1,2)를 계산

        int i = v * 3 + h; //2차원 인덱스(v,h)를 1차원 인덱스로 변환
        return i;
    }
    bool MyTurn()
    {
        bool bClick = Input.GetMouseButtonDown(0); //마우스 왼쪽 버튼이 눌렸는지 확인
        if (!bClick) // 왼쪽 버튼이 눌리지 않았다면 실패로 간주
        {
            return false;
        }
        Vector3 pos = Input.mousePosition; //마우스의 현재 화면 좌표를 가져온다.

        int i = PosToNumber(pos); // 마우스 좌표를 3x3 보드의 배열로 변환
        if (i == -1) // 마우스 클릭 위치가 영역 밖이라면 false를 반환
        {
            return false;
        }

        bool bSet = SetStone(i, stoneI); //해당 위치에 돌을 놓는다.
        if (bSet == false) // 이미 돌이 놓인 곳이어서 실패하면 false를 반환
        {
            return false;
        }

        byte[] data = new byte[1]; //상대방에게 보낼 1바이트 크기의 데이터 배열을 준비
        data[0] = (byte)i; //배열 인덱스 i를 1바이트 데이터로 저장
        tcp.Send(data, data.Length); //준비된 데이터를 상대방 네트워크에 전송

        Debug.Log("보냄: " + i); // 전송된 인덱스 정보를 콘솔에 출력

        return true; //돌을 성공적으로 놓았으므로  true 반환
    }

    public void ServerStart() //start 메서드에서 가져온 tcp 객체의 서버 시작 함수, 서버 역할과 플레이어 접속 기다림
    {
        tcp.StartServer(10000, 10); //포트 번호, 몇명의 클라이언트를 허용할지
    }

    public void ClientStart() //연결시도
    {
        tcp.Connect(ip.text, 10000); //ip주소를 가져오고, 포트 번호
    }

    void OnGUI()
    { //현재 처리중인 gui 이벤트, 화면을 다시 그려야할 이벤트 타입, 현재 이벤트 타입이 아니라면 함수 종료
        if (!Event.current.type.Equals(EventType.Repaint))
            return;
        Graphics.DrawTexture(new Rect(0, 0, 400, 400), //(0,0)좌표에 400x400 크기로 보드판 이미지 그림
      textBord);

        //화면에 직접 텍스처를 그려주는 메서드

        for (int i = 0; i < board.Length; ++i)
        {
            if (board[i] != (int)Stone.None) //현재 i에 돌이 놓여 있는지 확인 
            {
                float x = 50 + (i % 3) * 100; //가로(열) 위치를 결정
                float y = 50 + (i / 3) * 100; //세로(행) 위치를 결정

                Texture tex = (board[i] == (int)Stone.White) ? textWhite : textBlack; //현재 칸에 놓인 돌이 흰 돌인지 확인
                Graphics.DrawTexture(new Rect(x, y, 100, 100), tex); //흰돌이면 흰돌 텍스처를, 검은 돌이면 검은 돌을 그린다.
            }
        }

        if (state == State.Game) // 게임 중이라면
        {
            if (stoneTurn == Stone.White) //현재 턴이 흰돌인지 확인
                Graphics.DrawTexture(new Rect(0, 400, 100, 100), textWhite); //흰 돌의 턴이면 (x =0, y =400)에 흰돌 이미지를 그려 턴을 표시
            else
                Graphics.DrawTexture(new Rect(300, 400, 100, 100), textBlack); //검은 돌의 턴이라 오른쪽 하단(x =300, y =400)에 검은 돌 표시
        }

        if (state == State.End) //게임이 종료된다면
        {
            if (stoneWinner == Stone.White) //승자가 흰돌인지 확인 후 흰돌이면 중앙(x=150, y =400)에 흰 돌 이미지를 그린다.
                Graphics.DrawTexture(new Rect(150, 400, 100, 100), textWhite);
            else
                Graphics.DrawTexture(new Rect(150, 400, 100, 100), textBlack); //승자가 흰돌이 아니라면 검은 돌을 그림
        }
    }

}
