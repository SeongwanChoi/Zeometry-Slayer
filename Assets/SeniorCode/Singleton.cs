using UnityEngine;

//enum 관련은 못적음 -> 핵심은 리스트에 제네릭, 클래스를 형식으로 써서 데이터를 담는 변수처럼 쓸 수도 있으며 Find() FindAll() 함수를 사용하는 것이 핵심

public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    static T instance;

    public static T Ins
    {
        get
        {
            if (instance == null)
            {
                // 오브젝트 이름이 클래스 이름과 다르더라도(예: Canvas 안의 PlayerHand, AttackBox)
                // 씬에 이미 존재하는 T 컴포넌트를 먼저 찾아서 그대로 활용합니다.
                instance = Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);

                if (instance == null)
                {
                    GameObject obj = GameObject.Find(typeof(T).Name);
                    if (obj == null)
                    {
                        obj = new GameObject(typeof(T).Name);
                        instance = obj.AddComponent<T>();
                    }
                    else
                    {
                        instance = obj.GetComponent<T>();
                    }
                }
            }

            return instance;
        }
    }

    protected virtual void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this as T;
        // 루트 오브젝트일 때만 DontDestroyOnLoad 호출 (Canvas 하위 UI 오브젝트가 깨지지 않도록 보호)
        if (transform.parent == null)
        {
            DontDestroyOnLoad(gameObject);
        }
        Init();
    }

    public virtual void Init() { } // 호출하면 DontDestroy목록으로 생길테니까
}
