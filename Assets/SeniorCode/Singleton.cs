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
                GameObject obj;
                obj = GameObject.Find(typeof(T).Name);
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
        DontDestroyOnLoad(this);
        Init();
    }

    public virtual void Init() { } // 호출하면 DontDestroy목록으로 생길테니까
}
