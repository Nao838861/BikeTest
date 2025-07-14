using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Car : MonoBehaviour
{
    [System.Serializable]
    public class Spring
    {
        [HideInInspector]
        public Transform mTransform;
        [HideInInspector]
        public Rigidbody mRigidbody;
        public float mMaxLength;
        public float mLength;
        public Vector3 mLocalPos;
        public Vector3 mLocalVec;
        public Vector3 mWorldPos;
        public Vector3 mWorldVec;
        public Vector3 mContactPoint;
        public Vector3 mContactNormal;
        public float mSpringPow;

        public void init(Transform transform, Rigidbody rigidbody)
        {
            mRigidbody = rigidbody;
            mTransform = transform;
            mMaxLength = 1.0f;
            mLocalVec = new Vector3(0, -1, 0);
        }

        public void update()
        {
            mWorldPos = mTransform.TransformPoint(mLocalPos);
            mWorldVec = mTransform.TransformDirection(mLocalVec).normalized;

            RaycastHit hit;
            if (Physics.Raycast(mWorldPos, mWorldVec * mMaxLength, out hit, Mathf.Infinity, 65535, QueryTriggerInteraction.Ignore ) && hit.distance < mMaxLength)
            {
                float cSpring = 2000.0f;
                float cDumper = -180.0f;
                //print("Found an object - distance: " + hit.distance);
                mContactNormal = hit.normal;
                mLength = hit.distance;

                // ばね
                mSpringPow = (mMaxLength - mLength) * cSpring;
                mRigidbody.AddForceAtPosition(-mWorldVec * mSpringPow, mWorldPos);

                // ダンパー
                Vector3 spd = mRigidbody.GetPointVelocity(mWorldPos);
                float dirSpd = Vector3.Dot(spd, mWorldVec);
                float dmpPow = dirSpd * cDumper;
                mRigidbody.AddForceAtPosition(mWorldVec * dmpPow, mWorldPos);
            }
            else
            {
                mLength = mMaxLength;
            }
            //Debug.DrawLine(mWorldPos, mWorldPos + mWorldVec * mLength, Color.red);
        }

        public bool isOnGround()
        {
            if (mLength != mMaxLength) return true;
            else return false;
        }
    }

    // 線形補間(範囲外はクランプ)
    float linerInterpolate(float now, float start, float end, float startVal, float endVal)
    {
        if (now < start) return startVal;
        if (now > end) return endVal;
        float ratio = (now - start) / (end - start);
        return ratio * (endVal - startVal) + startVal;
    }

    [System.Serializable]
    public class Tyre
    {
        [HideInInspector] public Transform mTransform;
        [HideInInspector] public Rigidbody mRigidbody;
        public GameObject mTyreObj;
        public TyreDebug mTyreDebug;
        public Spring mSpring = new Spring();
        public Vector3 mWorldPos;
        public Vector3 mContactNormal;
        public Vector3 mTyreSpd;
        public Vector3 mTyreSideSpd;
        public Vector3 mCf;
        public Vector3 mAcc;
        public float mAccPow;
        public float mFrictionCircle;
        public float mTyrePowScr;
        public float mSlipAngle;
        public Car mCar;

        public void init(Transform transform, Rigidbody rigidbody, Car car)
        {
            mSpring.init(transform, rigidbody);

            mRigidbody = rigidbody;
            mTransform = transform;

            mCar = car;
        }

        // シグモイド関数
        float sigmoid(float gain, float x)
        {
            return 1.0f / (1.0f + Mathf.Exp(-gain * x));
        }

        // 線形補間(範囲外は補外)
        float linerInterpolate_noclamp( float now, float start, float end, float startVal, float endVal)
        {
            float ratio = (now - start) / (end - start);
            return ratio * (endVal - startVal) + startVal;
        }


        public void update()
        {

            mSpring.update();

            float length = mSpring.mLength;
            if (length < 0.5f) length = 0.5f;
             mWorldPos = mSpring.mWorldPos + mSpring.mWorldVec * length;
            mTyreObj.transform.position = mWorldPos - mSpring.mWorldVec * 0.35f;

            if (mTyreDebug)
            {
                //float pow = 1.0f - (mSpring.mLength / mSpring.mMaxLength);
                // 摩擦円の大きさ
                mFrictionCircle = mSpring.mSpringPow*2.0f + 10.0f;
                mFrictionCircle *= mCar.mFrictionCircleMul;
                if (mTyreDebug != null) mTyreDebug.mFrictionCirclePow = mFrictionCircle;
            }

            mTyreSpd = mRigidbody.GetPointVelocity(mWorldPos);
            // タイヤ速度から地面の法線成分を除去
            mTyreSpd -= mSpring.mContactNormal*Vector3.Dot(mSpring.mContactNormal, mTyreSpd);

            float tyrefrontDot = Vector3.Dot(mTyreSpd, mTyreObj.transform.forward);
            Vector3 tyrefrontSpd = mTyreObj.transform.forward * tyrefrontDot;
            mTyreSideSpd = mTyreSpd - tyrefrontSpd;
            if (mSpring.isOnGround())
            {
                mContactNormal = mSpring.mContactNormal;

                // スリップアングル計算
                float tyreDot = Vector3.Dot(mTyreSpd.normalized, mTyreObj.transform.forward);
                mSlipAngle = Mathf.Acos(tyreDot);

                // スリップアングルの符号を求める
                float sign = 1.0f;
                Vector3 crs = Vector3.Cross(mTyreSpd.normalized, mTyreObj.transform.up);
                if (Vector3.Dot(crs, mTyreObj.transform.forward) < 0.0f) sign = -1.0f;

                if (mTyreDebug != null) mTyreDebug.mSlipAngle = mSlipAngle * 360.0f / (2.0f*Mathf.PI) * sign;
                float cfCurve = mSlipAngle / 10.0f; // 10度までは線形にコーナリングフォースが増えて、そこでクランプ
                cfCurve = sigmoid(1.0f, cfCurve);
                if (cfCurve > 1.0f) cfCurve = 1.0f;
                float cfMax = cfCurve * 3000.0f;

                // コーナリングフォース計算
                float pow = 60.0f;
                Vector3 cf = -mTyreSideSpd * pow;

                // コーナリングフォースの最大値制限
                if( cf.magnitude > cfMax)
                {
                    cf = cf.normalized * cfMax;
                }

                //cf -= mContactNormal * Vector3.Dot(cf, mContactNormal);
                //float dot = Vector3.Dot(force, mTyreObj.transform.right);
                mCf = mTyreObj.transform.InverseTransformVector(cf);
                if (mTyreDebug != null) mTyreDebug.mCf = mCf;
                //Debug.DrawLine(mWorldPos, mWorldPos + cf, Color.green);

                // アクセル反映
                Vector3 accForce = mTyreObj.transform.forward * mAccPow * 40;
                accForce -= mContactNormal * Vector3.Dot(accForce, mContactNormal);
                mAcc = mTyreObj.transform.InverseTransformVector(accForce);
                if (mTyreDebug != null) mTyreDebug.mAcc = mAcc;


                // 摩擦円を反映
                float mu = 1.0f;
                Vector3 sumForce = accForce + cf;
                mTyrePowScr = sumForce.magnitude;
                float ratio = mTyrePowScr / mFrictionCircle;
                if (ratio > 1.0f)
                {
                    float r = ratio - 1.0f;
                    //if (r > 1.0f) r = 1.0f;
                    float mu_min = 0.16f;
                    mu = mu_min;// + (0.7f*r*r*r);

                    float p0 = 1.02f;
                    float p1 = 1.06f;
                    float r0 = 1.02f;
                    if (     ratio > 1.00f && ratio < p0) mu = linerInterpolate_noclamp(ratio, 1.00f, p0, 1.00f, r0);
                    else if (ratio > p0    && ratio < p1) mu = linerInterpolate_noclamp(ratio, p0,    p1, r0,    mu_min);
                    else                                  mu = mu_min;
                    
                }
                    sumForce *= mu;
                if (mTyreDebug != null) mTyreDebug.mMu = mu;
                mRigidbody.AddForceAtPosition(sumForce, mWorldPos);
                //mRigidbody.AddForceAtPosition(accForce, mWorldPos);
                //mRigidbody.AddForceAtPosition(mCf, mWorldPos);

            }

        }

        public void accel(float pow)
        {
            if ( mSpring.isOnGround() )
            {
                mAccPow = pow;
                /*
                mContactNormal = mSpring.mContactNormal;
                Vector3 force = mTransform.forward * pow;
                force -= mContactNormal * Vector3.Dot(force, mContactNormal);
                mRigidbody.AddForceAtPosition(force, mWorldPos);

                mTyreDebug.mAcc = force;
                */
            }
            else
            {
                mAccPow = 0;
                //mTyreDebug.mAcc = new Vector3(0, 0, 0);
            }
        }
    }

    public bool mIsDownHill;
    public TextMesh mTextSpeed;
    public TextMesh mTextTime;
    public TextMesh mTextLap;
    public Tyre[] mTyreAry = new Tyre[4];
    public float mHandle;
    public float mFrontToe;
    public float mBackToe;

    float mAccPow;
    float mCarSpeed;

    float mStartWaitTimer;
    bool mIsStart;
    bool mIsGoal;
    public float mTime;
    int mCheckNum;
    public float[] mGateTime = new float[5];


    public AudioSource[] mSndTyreSlip = new AudioSource[4];
    public AudioSource[] mSndTyreBrake = new AudioSource[2];
    public AudioSource mSndTyreRoll;
    public AudioSource mSndEngine;

    //GameMgr mGameMgr;

    public float mAccMul = 1.0f;
    public float mFrictionCircleMul = 1.0f;
    // Use this for initialization
    void Start()
    {
        //mGameMgr = GameObject.Find("GameMgr").GetComponent<GameMgr>();
        /*
        if ( mIsDownHill)
        {
            transform.position = new Vector3(-99.21f, 102.8f, -52.76f);
            transform.localEulerAngles = new Vector3(4.055f, -143, -0.146f);
        }
        else
        {
            transform.position = new Vector3(330.0f, -22.8f, -305.0f);
            transform.localEulerAngles = new Vector3(-3.5f,34.0f, 0.0f);
        }
        */
        for (int i = 0; i < mTyreAry.Length; i++)
        {
            mTyreAry[i].init(transform, GetComponent<Rigidbody>(), this);
        }

        mStartWaitTimer = 0.1f;
    }

    string getTimeText(float time)
    {
        int min = (int)time / 60;
        float sec = (float)time - min * 60;

        string ret = string.Format("{0, 2}", min) + "'" + sec.ToString("00.000");
        return ret;

    }

    public float[] mTyreSlipVolume = new float[4];
    public float[] mTyreBrakeVolume = new float[2];
    public float mTyreRollVolume;
    public float mEngineVolume;

    public float mVt;

    // ひっくり返った時の復帰位置
    public float mNoSafeTimer;
    public Vector3 mLastSafePos;
    public Vector3 mLastSafeRot;


    // Update is called once per frame
    void FixedUpdate()
    {
        // スタート待機中
        if(mStartWaitTimer > 0)
        {
            mStartWaitTimer -= Time.deltaTime;
            /*
            if(mIsDownHill) 
            {
                transform.position = new Vector3(-99.21f, 102.8f, -52.76f);
                transform.localEulerAngles = new Vector3(4.055f, -143, -0.146f);
            }else
            {
                transform.position = new Vector3(330.0f, -22.8f, -305.0f);
                transform.localEulerAngles = new Vector3(-3.5f, 34.0f, 0.0f);
            }
            */
            GetComponent<Rigidbody>().velocity = new Vector3(0, 0, 0);
        }

        /*
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                transform.position = new Vector3(-99.21f, 102.8f, -52.76f);
                transform.localEulerAngles = new Vector3(4.055f, -143, -0.146f);
            }
            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                transform.position = new Vector3(330.0f, -22.8f, -305.0f);
                transform.localEulerAngles = new Vector3(-3.5f, 34.0f, 0.0f);
            }

        }
        */

        if ( mIsStart && !mIsGoal)
        {
            mTime += Time.deltaTime;
            mTextTime.text = getTimeText(mTime);
        }


        for (int i = 0; i < mTyreAry.Length; i++)
        {
            mTyreAry[i].update();
        }

        float handleSpd = 0.5f;

        float spdHandleOffset = 40.0f;
        if( mCarSpeed < spdHandleOffset)
        {
            handleSpd += (spdHandleOffset - mCarSpeed) / spdHandleOffset;
        }

        float handle = Input.GetAxis("Horizontal");
        mVt = handle;
        float handleMax = 30.0f;
        float handleRatio = (Mathf.Abs(handle) - 0.5f)*2.0f;
        if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A) || handle < -0.5f )
        {
            mHandle -= handleSpd;
            if (mHandle < -handleMax) mHandle = -handleMax;
            if( handle < -0.5f)
            {
                if (mHandle < -handleMax* handleRatio) mHandle += handleSpd*1.1f;
            }
        }
        else if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D) || handle > 0.5f)
        {
            mHandle += handleSpd;
            if (mHandle > handleMax) mHandle = handleMax;
            if (handle > 0.5f)
            {
                if (mHandle > handleMax * handleRatio) mHandle -= handleSpd * 1.1f;

            }
        }
        else
        {
            mHandle *= 0.95f;
        }
        mTyreAry[0].mTyreObj.transform.localEulerAngles = new Vector3(0, mHandle + mFrontToe, 0);
        mTyreAry[1].mTyreObj.transform.localEulerAngles = new Vector3(0, mHandle - mFrontToe, 0);

        mTyreAry[2].mTyreObj.transform.localEulerAngles = new Vector3(0, mBackToe, 0);
        mTyreAry[3].mTyreObj.transform.localEulerAngles = new Vector3(0, -mBackToe, 0);

        bool isAcc = false;
        bool isBrk = false;
        float acc = 0;// Input.GetAxis("Acc");
        float brk = 0;//Input.GetAxis("Brk");

        float accPow = 0.0f;
        if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.Space) || acc != 0 )
        {
            isAcc = true;
            accPow = 3.0f;

            // アクセルを時速30Kmまでは加速する仕様に！
            float maxSpd = 30.0f;
            if (mCarSpeed < maxSpd)
            {
                float addPow = maxSpd - mCarSpeed;
                accPow += addPow * 0.3f;
            }
            if (acc != 0) accPow *= acc;

            if(mStartWaitTimer <= 0)
            {
                mTyreAry[0].accel(0);
                mTyreAry[1].accel(0);
                mTyreAry[2].accel(mAccPow);
                mTyreAry[3].accel(mAccPow);
                /*
                mTyreAry[0].accel(mAccPow);
                mTyreAry[1].accel(mAccPow);
                mTyreAry[2].accel(0);
                mTyreAry[3].accel(0);
                */
            }
        }
        else if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S) || brk != 0)
        {
            isBrk = true;
            accPow = -6.0f;
            // アクセルを時速30Kmまでは加速する仕様に！
            float maxSpd = 30.0f;
            if (mCarSpeed < maxSpd)
            {
                float addPow = maxSpd - mCarSpeed;
                accPow -= addPow * 0.3f;
            }
            if (brk != 0) accPow *= brk;


            /*
            mTyreAry[0].accel(mAccPow);
            mTyreAry[1].accel(mAccPow);
            mTyreAry[2].accel(mAccPow);
            mTyreAry[3].accel(mAccPow);
            */
            if (mStartWaitTimer <= 0)
            {
                mTyreAry[0].accel(mAccPow);
                mTyreAry[1].accel(mAccPow);
                mTyreAry[2].accel(0);
                mTyreAry[3].accel(0);
            }
        }
        else
        {
            mTyreAry[0].accel(0);
            mTyreAry[1].accel(0);
            mTyreAry[2].accel(0);
            mTyreAry[3].accel(0);
        }
        accPow *= mAccMul;
        mAccPow += (accPow - mAccPow) * 0.01f;
        if( isBrk == false && mAccPow < 0)
        {
            mAccPow *= 0.9f;
        }

        mCarSpeed = GetComponent<Rigidbody>().velocity.magnitude;
        mCarSpeed = mCarSpeed * 60 * 60 / 1000;
        mTextSpeed.text = mCarSpeed.ToString("F1") + " Km/h";

        // ダウンフォース
        float downForce = mCarSpeed * -3.1f;
        GetComponent<Rigidbody>().AddForceAtPosition(new Vector3(0,1,0)* downForce, transform.position);

        // タイヤスリップ音
        for ( int i=0; i< 4; i++)
        {
            float volume = 0;
            float ratio = mTyreAry[i].mTyrePowScr / mTyreAry[i].mFrictionCircle;
            float min = 0.60f;
            if (ratio > min) volume = (ratio - min) * 30.0f;
            if (volume > 1.0f) volume = 1.0f;
            if (!mTyreAry[i].mSpring.isOnGround()) volume = 0;

            // 時速60Km以下は音量を抑えるが、時速30Kmで0.2倍でクランプ
            volume = volume * linerInterpolate(mCarSpeed, 30.0f, 60.0f, 0.2f, 1.0f);
            
            // 加速中は時速35Kmまでは後輪にスリップ音をうっすら入れる
            if ( i >= 2 && accPow != 0.0f)
            {
                //volume += linerInterpolate(mCarSpeed, 0.0f, 60.0f, 0.30f, 0.00f);
            }


            if (mTyreSlipVolume[i] < volume)    mTyreSlipVolume[i] += (volume - mTyreSlipVolume[i]) * 0.1f;
            else                                mTyreSlipVolume[i] += (volume - mTyreSlipVolume[i]) * 0.45f;
            mSndTyreSlip[i].volume = 1.0f*mTyreSlipVolume[i];
            float pitch = mTyreAry[i].mTyrePowScr;
            mSndTyreSlip[i].pitch = 0.70f + (pitch-80.0f)/2200.0f;
        }

        // タイヤブレーキ音
        if(isBrk){
            float ratio0 = mTyreAry[0].mTyrePowScr / mTyreAry[0].mFrictionCircle;
            float ratio1 = mTyreAry[1].mTyrePowScr / mTyreAry[1].mFrictionCircle;
            float ratio2 = mTyreAry[2].mTyrePowScr / mTyreAry[2].mFrictionCircle;
            float ratio3 = mTyreAry[3].mTyrePowScr / mTyreAry[3].mFrictionCircle;
            float ratio01 = Mathf.Max(ratio0, ratio1);
            float ratio23 = Mathf.Max(ratio2, ratio3);

            float speedRatio = 0.3f + (mCarSpeed / 100.0f) * 0.7f;
            float volFront = 0.250f * speedRatio;
            float volBack = 0.250f * speedRatio;
            if (ratio01 < 0.5f) volFront *= 0.7f;
            if (ratio23 < 0.5f) volBack *= 0.7f;
            mTyreBrakeVolume[0] += (volFront - mTyreBrakeVolume[0])*0.10f;
            mTyreBrakeVolume[1] += (volBack - mTyreBrakeVolume[1]) * 0.10f;
        }
        else {
            mTyreBrakeVolume[0] *= 0.2f;
            mTyreBrakeVolume[1] *= 0.2f;

            // 加速中は時速25Kmまでは後輪にブレーキ音をうっすら入れる
            if(accPow != 0.0f)
            {
                mTyreBrakeVolume[1] += linerInterpolate(mCarSpeed, 0.0f, 25.0f, 0.10f, 0.0f); ;
            }
        }
        mSndTyreBrake[0].volume = 1.0f * mTyreBrakeVolume[0];
        mSndTyreBrake[1].volume = 1.0f * mTyreBrakeVolume[1];

        float brkSpeedRatio = 0.5f + (mCarSpeed / 100.0f) * 0.5f;
        float pitchFront = 0.5f * (mTyreAry[0].mTyrePowScr + mTyreAry[1].mTyrePowScr);
        float pitchBack  = 0.5f * (mTyreAry[1].mTyrePowScr + mTyreAry[2].mTyrePowScr);
        mSndTyreBrake[0].pitch = 0.60f + (pitchFront - 40.0f) / 2000.0f * brkSpeedRatio;
        mSndTyreBrake[1].pitch = 0.60f + (pitchBack - 40.0f) / 2000.0f * brkSpeedRatio;

        // タイヤ転がり音
        if ( mTyreAry[0].mSpring.isOnGround() || mTyreAry[1].mSpring.isOnGround() || mTyreAry[2].mSpring.isOnGround() || mTyreAry[3].mSpring.isOnGround())
        {
            mTyreRollVolume = mCarSpeed / 80.0f;
        }else
        {
            mTyreRollVolume *= 0.1f;
        }
        float rollvol = mTyreRollVolume*0.05f;
        if (rollvol > 0.05f) rollvol = 0.05f;
        mSndTyreRoll.volume = rollvol;
        mSndTyreRoll.pitch = mTyreRollVolume * 3.0f;

        // エンジン音
        if (isAcc)
        {
            float max = 1.0f;
            if (acc != 0) max = acc;
            mEngineVolume += (max - mEngineVolume) * 0.1f;
        }
        else mEngineVolume *= 0.07f;
        float engineVolBase = (mCarSpeed / 80.0f) * 0.25f;
        float engineVol = engineVolBase;
        if (engineVol > 0.25f) engineVol = 0.25f;
        if (engineVol < 0.20f) engineVol = 0.20f;
        mSndEngine.volume = engineVol;
        mSndEngine.pitch = engineVolBase * 10.0f + mEngineVolume * 2.0f;

        // ひっくり返った時の復帰処理
        int tyreGroundNum = 0;
        for(int i=0; i<4; i++) if (mTyreAry[i].mSpring.isOnGround()) tyreGroundNum++;
        if(tyreGroundNum == 4)
        {
            mNoSafeTimer = 0.0f;
            mLastSafePos = transform.position;
            mLastSafeRot = transform.eulerAngles;
        }else
        {
            mNoSafeTimer+=Time.deltaTime;
            if(mNoSafeTimer > 4.0f)
            {
                mNoSafeTimer = 0.0f;
                transform.position = mLastSafePos;
                transform.eulerAngles = mLastSafeRot;
            }
        }

    }

    void OnCollisionEnter(Collision collision)
    {
        foreach (ContactPoint point in collision.contacts)
        {
            //衝突位置
            float ratio = -0.1f + mCarSpeed / 100.0f * 2.2f;

            Debug.Log(point);
            float force = Random.Range(1000.0f, 2000.0f) * ratio;
            if (Random.Range(0.0f, 1.0f) < 0.5f) force *= -1;
            GetComponent<Rigidbody>().AddForceAtPosition(new Vector3(0, 1, 0) * force, point.point);

            float force2 = Random.Range(-1500.0f, 2500.0f) * ratio;
            GetComponent<Rigidbody>().AddForceAtPosition(point.normal * force2, point.point);

            float f = Mathf.Abs(force) + Mathf.Abs(force2);
            if (false)
            {
                // 衝突音の再生処理は削除
                // 必要に応じて後で実装

            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // ゴールしたらそれ以降は何もしない
        if (mIsGoal) return;

        if (mIsDownHill  && other.gameObject.tag == "Goal" ||
            !mIsDownHill && other.gameObject.tag == "Start" )
        {
            mIsStart = true;
        }
        if (mIsDownHill && other.gameObject.tag == "Start" ||
            !mIsDownHill && other.gameObject.tag == "Goal")
        {
            mIsGoal = true;
            //mGameMgr.SetGoal();
        }

        if (
            mIsDownHill &&
            (
                (mCheckNum == 0 && other.gameObject.tag == "Gate5") ||
                (mCheckNum == 1 && other.gameObject.tag == "Gate4") ||
                (mCheckNum == 2 && other.gameObject.tag == "Gate3") ||
                (mCheckNum == 3 && other.gameObject.tag == "Gate2") ||
                (mCheckNum == 4 && other.gameObject.tag == "Gate1")
            ) ||
            !mIsDownHill &&
            (
                (mCheckNum == 0 && other.gameObject.tag == "Gate1") ||
                (mCheckNum == 1 && other.gameObject.tag == "Gate2") ||
                (mCheckNum == 2 && other.gameObject.tag == "Gate3") ||
                (mCheckNum == 3 && other.gameObject.tag == "Gate4") ||
                (mCheckNum == 4 && other.gameObject.tag == "Gate5")
            ))
        {
            mGateTime[mCheckNum] = mTime;
            mCheckNum++;

            //mGameMgr.setCheckPoint(mCheckNum-1);


            mTextLap.text = "";
            for (int i = 0; i < mCheckNum; i++)
            {
                Debug.Log(mGateTime[i]);
                mTextLap.text += getTimeText(mGateTime[i]) + "\n";
            }
        }
    }

}









/*
https://dova-s.jp/bgm/play3462.html
https://dova-s.jp/bgm/play4027.html

https://dova-s.jp/bgm/play3462.html

https://dova-s.jp/bgm/play4369.html 

 https://dova-s.jp/bgm/play3237.html **ボス曲
 https://dova-s.jp/bgm/play3178.html ** レース曲

     */


/*
決定音

    プレイ時間を記録
ゲームパッドの有無と日付を記録する

ゴール時のタイムが見にくいのを治す
 
ボイス

UIをフォントも含めて綺麗に

webGLのチェックポイントがおかしいのを治す


スタート直後に時間が進まないやつを治す

ランキング自分のが見えるように


チェックポイントで時間差分を出す

ツイッター連携追加(アップデート後に)
テストコースの追加

-------------------------------------------
摩擦円の数字を消す
escで戻るとフルスクリーンが解除される

(OK)カメラ切り替えを保存

(OK)BGMを容量削減して入れる
(OK)操作説明とXBoxパッド補足


(OK)ダメージ音
(OK)WASD操作対応
 
(OK)スタートカウントダウンがずれてるのを治す
     
(OK)4輪とも接触してた最終位置を覚えておいて、４輪接触が一定時間以上成立しなくなったらそこに戻す
(OK)ネットランキング
(OK)スタートカメラを治す
(OK)ゴール演出
(OK)スタート演出
*/


/*
 * BGMの一部
 * http://musicisvfr.com/free/license.html
*/

// KidTak 53/8 ダウンヒル   140.2331 
//         1/1  ヒルクライム 156.8167
// k0rin   1/1  ダウンヒル   187.9233 32.4  66.5  96.9 127.2 158.0
//         2/2  ダウンヒル   152.7358 29.1  54.2  74.0  96.7 127.7
//         9/5  ダウンヒル   146.4945 24.2  48.2  70.2  92.8 114.7
//        10/6  ダウンヒル   145.0342 24.7  51.3  70.8  90.8 118.8
//        13/7  ダウンヒル   140.0131 23.5  49.5  69.7  93.4 115.2
//        30/9  ダウンヒル   135.0720 23.3  48.4  68.0  91.2 112.3
//         1/1  ヒルクライム 169.0593
//         2/2  ヒルクライム 162.0178
//        14/5  ヒルクライム 145.9144 26.8  51.7  75.0  92.2 118.9
//        37/11 ヒルクライム 142.1736 26.6  49.9  71.8  90.1 116.8
/*
配信
https://www.cavelis.net/view/AFDF5B4458CC453AA3B52DD48EB19284

https://www.cavelis.net/view/7AF7DF677A04426AA92BC83E0075FC52

*/
