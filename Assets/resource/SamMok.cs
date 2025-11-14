using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class SamMok : MonoBehaviour

{
    Tcp tcp; //통신용 변수 tcp, tp변수 선언
    public InputField ip;
    public Texture textBord;
    public Texture textWhite;
    public Texture textBlack;

    //바둑돌 관련 변수
    int[] board = new int[9]; // 9칸의 돌의 상태를 저장(none, white, black)

    State state; //게임 진행 상태 start, game, end

    Stone stoneTurn; //현재 턴을 가진 플레이어의 돌 색깔
    Stone stoneI; //나의 돌의 색깔
    Stone stoneYou; //상대방의 돌 색깔
    Stone stoneWinner; //승자의 돌 색깔
 
    enum State //enum은 열거형 이름이 있는 상수들의 집합을 정의
    {
        Start = 0, //start는 명시적으로 0을 할당
        Game, //start의 값에 1이 증가된 값인 1이 자동 할당
        End, //Game의 값에 1이 증가된 값인 2가 자동 할당
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
        Black
    }

    private void Start()
    {
        tcp = GetComponent<Tcp>(); //tcp 타입의 컴포넌트를 찾아 그 인스턴스를 가져와  tcp에 할당

        state = State.Start; //게임 중비 상태

        for(int i = 0; i < board.Length; ++i)
        {
            board[i] = (int)Stone.None; //게임 보드 초기화
        }
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
    { //현재 처리중인 gui 이벤트, 화면을 다시 그려야할 이벤트 타입, 현재 이베튼 타입이 아니라면 함수 종료
        if (!Event.current.type.Equals(EventType.Repaint))
            return;
        Graphics.DrawTexture(new Rect(0, 0, 400, 400),
      textBord); 

        //화면에 직접 텍스처를 그려주는 메서드
    }

    }
