/**
FinalIKを使ったLeapMotion Orion用HandController
 (VRIKバージョン)
Author: MiyuMiyu (https://twitter.com/miyumiyuna5)
Source: https://qiita.com/miyumiyu/items/72b965df46a79f3ec523
Modified by: Emiliana (https://twitter.com/Emiliana_vt)
Modifications: Updated for current SDK version, supports hand position reset on tracking loss, hand mirroring and interpolation.
*/

/*
`LeapMotion\Core\Scripts\Hands\RiggedHands.cs` needs to be modified slightly. `jointList` and `localRotations` need to be set to `public` rather than `private`.

`LeapMotion\Core\Scripts\LeapProvider.cs` needs to have the following method added to the `LeapProvider` class:

    public void ClearHandlers() {
        OnFixedFrame = null;
        OnUpdateFrame = null;
    }
*/

using AutoRigType = Leap.Unity.LeapHandsAutoRig;
using RiggedHandType = Leap.Unity.RiggedHand;
using RiggedFingerType = Leap.Unity.RiggedFinger;

using System;
using System.Collections.Generic;
using UnityEngine;
using Leap;
using Leap.Unity;
using RootMotion.FinalIK;

public class FinalIKOrionLeapHandController : HandModelManager {
    [SerializeField]
    public VRIK vrIK;
    
    public Transform reference = null;
    
    public bool track = false;
    public bool leftActive = false;
    public bool rightActive = false;
    public bool swap = false;
    public bool applyMirror = false;
    // This is actually not used because the Leap Motion SDK always returns 1 anyways.
    public float confidenceThreshold = 0.85f;
    public int skip = 0;

    public float smoothing = 0.5f;
    public float gracePeriod = 0.25f;
    public float lerpPeriodIn = 0.4f;
    public float lerpPeriodOut = 1.25f;
    
    public bool rangeLimit = true;
    public float maximumRange = 0.5f;

    [HideInInspector]
    public HandModel leftHand;
    [HideInInspector]
    public HandModel rightHand;
    
    private AutoRigType autoRig;
    private bool initialized = false;
    private RiggedHandType l = null;
    private RiggedHandType r = null;
    private HashSet<Transform> fingerBones;
    
    private float firstGotLeft = -10f;
    private float firstGotRight = -10f;
    private float lastLeft = -10f;
    private float lastRight = -10f;
    private List<Quaternion> lastLeftRotations;
    private List<Quaternion> lastRightRotations;
    private List<Quaternion> newLeftRotations;
    private List<Quaternion> newRightRotations;
    private List<Quaternion> lastGoodLeftRotations;
    private List<Quaternion> lastGoodRightRotations;
    private bool gotLeft = false;
    private bool gotRight = false;
    private Vector3 leftPos = Vector3.zero;
    private Vector3 rightPos = Vector3.zero;
    private Quaternion leftRot = Quaternion.identity;
    private Quaternion rightRot = Quaternion.identity;
    private Vector3 lastLeftPos = Vector3.zero;
    private Vector3 lastRightPos = Vector3.zero;
    private Quaternion lastLeftRot = Quaternion.identity;
    private Quaternion lastRightRot = Quaternion.identity;
    private int interpolateState = 0;
    private int interpolationCount = 1;
    private float avgInterps = 1f;
    private bool lastMirror = false;
    private bool failed = false;

    // Ghost hunting
    private int leftAliveCount = 0;
    private int rightAliveCount = 0;
    private List<Quaternion> lastRawLeftRotations = null;
    private List<Quaternion> lastRawRightRotations = null;
    private Vector3 lastRawLeftPosition = Vector3.zero;
    private Vector3 lastRawRightPosition = Vector3.zero;

    private int leftBad = 0;
    private int rightBad = 0;
    
    private float dtLeftIn = 0f;
    private float dtLeftOut = 0f;
    private float dtRightIn = 0f;
    private float dtRightOut = 0f;

    public void Initialize() {
        autoRig = vrIK.gameObject.GetComponent<AutoRigType>();
        if (autoRig == null)
            autoRig = vrIK.gameObject.AddComponent<AutoRigType>() as AutoRigType;
        RemoveGroup("VRM");
        RemoveGroup("EmilianaCecil");
        autoRig.ModelGroupName = "VRM";
        failed = false;
        try {
            autoRig.AutoRig();
        } catch (Exception e) {
            Debug.LogWarning("AutoRig failed: " + e);
            failed = true;
            return;
        }
        leftHand = autoRig.RiggedHand_L;
        rightHand = autoRig.RiggedHand_R;
        Destroy(leftHand.gameObject.GetComponent<HandDrop>());
        Destroy(rightHand.gameObject.GetComponent<HandDrop>());
        fingerBones = new HashSet<Transform>();
        l = null;
        r = null;
        if (leftHand) {
            Quaternion handRot = leftHand.transform.rotation;
            leftHand.transform.rotation = Quaternion.identity;
            foreach (var finger in leftHand.fingers) {
                if (finger == null)
                    continue;
                RiggedFingerType riggedFinger = finger as RiggedFingerType;
                foreach (var bone in finger.bones) {
                    fingerBones.Add(bone);
                }
                if (finger.fingerType == Leap.Finger.FingerType.TYPE_THUMB) {
                    riggedFinger.modelFingerPointing = new Vector3(-1, 0, 1);
                }
                int i;
                for (i = 0; i < 4; i++) {
                    if (finger.bones[i] != null)
                        break;
                }
                if (i < 3) {
                    // Thanks to https://twitter.com/Virtual_Deat for this!
                    if (finger.bones[i+1] != null && finger.bones[i] != null)
                        riggedFinger.modelFingerPointing = (finger.bones[i+1].position - finger.bones[i].position).normalized;
                }
            }
            leftHand.transform.rotation = handRot;
            l = leftHand as RiggedHandType;
        }
        if (rightHand) {
            Quaternion handRot = rightHand.transform.rotation;
            rightHand.transform.rotation = Quaternion.identity;
            foreach (var finger in rightHand.fingers) {
                if (finger == null)
                    continue;
                RiggedFingerType riggedFinger = finger as RiggedFingerType;
                foreach (var bone in finger.bones) {
                    fingerBones.Add(bone);
                }
                if (finger.fingerType == Leap.Finger.FingerType.TYPE_THUMB) {
                    riggedFinger.modelFingerPointing = new Vector3(1, 0, 1);
                }
                int i;
                for (i = 0; i < 4; i++) {
                    if (finger.bones[i] != null)
                        break;
                }
                if (i < 3) {
                    if (finger.bones[i+1] != null && finger.bones[i] != null)
                        riggedFinger.modelFingerPointing = (finger.bones[i+1].position - finger.bones[i].position).normalized;
                }
            }
            rightHand.transform.rotation = handRot;
            r = rightHand as RiggedHandType;
        }
    }

    protected virtual void Awake() {
        if (!initialized) {
            Initialize();
            initialized = true;
        }
        if (vrIK == null) {
            vrIK = gameObject.transform.root.GetComponent<VRIK>();
        }
        if (vrIK == null) {
            Debug.LogError("FinalIKOrionLeapHandController:: no FullBodyBipedIK found on GameObject or any of its parent transforms. ");
        }

        if (leftHand == null)
            Debug.LogError("IKOrionLeapHandController::Awake::No Rigged Hand set for left hand parameter. You have to set this in the inspector.");
        if (rightHand == null)
            Debug.LogError("IKOrionLeapHandController::Awake::No Rigged Hand set for right hand parameter. You have to set this in the inspector.");

        // Physic Handは使用しないのでDisableにする
        physicsEnabled = false;
    }

    protected virtual void Start() {
        leapProvider = GetComponent<LeapProvider>();
        if (leapProvider == null) {
            Debug.LogError("IKOrionLeapHandController::Start::No Leap Provider component was present on " + gameObject.name);
            Debug.Log("Added a Leap Service Provider with default settings.");
            leapProvider = gameObject.AddComponent<LeapServiceProvider>() as LeapProvider;
        }
        leapProvider.ClearHandlers();
    }

    Quaternion MirrorQuaternion(Quaternion q) {
        return new Quaternion(-q.x, q.y, q.z, -q.w);
    }

    Vector3 MirrorTranslation(Vector3 v) {
        return new Vector3(-v.x, v.y, v.z);
    }
    
    Vector3 LocalSpace(Vector3 v) {
        return v - transform.position;
    }
    
    Vector3 GlobalSpace(Vector3 v) {
        return v + transform.position;
    }
    
    List<Quaternion> GetJointRotations(RiggedHandType hand) {
        if (hand == null)
            return null;
        List<Quaternion> fingerRots = new List<Quaternion>();
        foreach (var t in hand.jointList) {
            if (t == null || !fingerBones.Contains(t))
                continue;
            fingerRots.Add(t.localRotation);
        }
        return fingerRots;
    }
    
    void SetJointRotations(RiggedHandType hand, List<Quaternion> rotationsFrom, List<Quaternion> rotationsTo, float factor, bool mirror) {
        if (hand == null)
            return;
        int i = 0;
        foreach (var t in hand.jointList) {
            if (t == null || !fingerBones.Contains(t) || i >= rotationsFrom.Count || i >= rotationsTo.Count)
                continue;
            Quaternion r = Quaternion.Lerp(rotationsFrom[i], rotationsTo[i], factor);
            i++;
            if (mirror)
                r = MirrorQuaternion(r);
            t.localRotation = r;
        }
    }
    
    void FadeJointRotations(RiggedHandType hand, List<Quaternion> rotations, float dt) {
        int i = 0;
        if (hand == null || rotations == null)
            return;
        foreach (var t in hand.jointList) {
            if (t == null || !fingerBones.Contains(t) || i >= rotations.Count || i >= hand.localRotations.Count)
                continue;
            t.localRotation = Quaternion.Lerp(rotations[i], hand.localRotations[i], dt);
            i++;
        }
    }
    
    public void LateUpdate() {
        if (failed || skip > 0) {
            if (skip > 0)
                skip--;
            return;
        }
        if (graphicsEnabled) {
            Vector3 preReferencePos = transform.position;
            Quaternion preReferenceRot = transform.rotation;
            
            if (reference != null) {
                transform.position = reference.position;
                transform.rotation = reference.rotation;
            }
            
            lastLeftRotations = newLeftRotations;
            lastRightRotations = newRightRotations;
            if (lastGoodLeftRotations == null)
                lastGoodLeftRotations = GetJointRotations(l);
            if (lastGoodRightRotations == null)
                lastGoodRightRotations = GetJointRotations(r);
            
            UpdateHandRepresentations();
            
            Vector3 rightPalmPos = Vector3.zero;
            Vector3 leftPalmPos = Vector3.zero;
            if (leftAliveCount > 0)
                leftPalmPos = leftHand.GetWristPosition();
            if (rightAliveCount > 0)
                rightPalmPos = rightHand.GetWristPosition();

            newLeftRotations = GetJointRotations(l);
            newRightRotations = GetJointRotations(r);
            List<Quaternion> rawLeftRotations = new List<Quaternion>();
            List<Quaternion> rawRightRotations = new List<Quaternion>();
            SetJointRotations(l, lastGoodLeftRotations, lastGoodLeftRotations, 0.5f, false);
            SetJointRotations(r, lastGoodRightRotations, lastGoodRightRotations, 0.5f, false);

            int badThreshold = 10;
            int goodFactor = 2;
            int badFactor = 1;
            int maxBadness = 112;
            float angularThreshold = 5f;
            float distanceThresholdHigh = 0.013f;
            float distanceThresholdLow = 0.001f;
            float jumpThreshold = 1f * maximumRange / 2f + 0.05f;

            float angularDiffRight = 0f;
            for (int i = 0; newRightRotations != null && lastRightRotations != null && i < newRightRotations.Count && i < lastRightRotations.Count; i++) {
                rawRightRotations.Add(newRightRotations[i]);
                if (lastRawRightRotations != null && rawRightRotations != null && lastRawRightRotations.Count == rawRightRotations.Count)
                    angularDiffRight += Mathf.Abs(Quaternion.Angle(lastRawRightRotations[i], rawRightRotations[i]));
                newRightRotations[i] = Quaternion.Lerp(lastRightRotations[i], newRightRotations[i], 1f - smoothing);
            }
            lastRawRightRotations = rawRightRotations;
            if (newRightRotations != null)
                angularDiffRight /= (float)newRightRotations.Count;
            float rightDist = Vector3.Distance(LocalSpace(rightPalmPos) / transform.lossyScale.x, lastRawRightPosition);
            if (angularDiffRight > angularThreshold && (rightAliveCount == 1 || (distanceThresholdLow < rightDist && rightDist < distanceThresholdHigh)))
                rightBad = Math.Min(maxBadness, rightBad + badFactor + Math.Max(0, 6 - rightAliveCount));
            else
                rightBad = Math.Max(0, rightBad - goodFactor);
            if (rangeLimit && rightDist > jumpThreshold && rightAliveCount > 2) {
                rightBad = Math.Min(maxBadness, rightBad + badThreshold);
            }
            if (rangeLimit && rightAliveCount == 1 && Vector3.Distance(LocalSpace(rightPalmPos) / transform.lossyScale.x, Vector3.zero) > maximumRange) {
                rightAliveCount = 0;
                rightActive = false;
            }
            lastRawRightPosition = LocalSpace(rightPalmPos) / transform.lossyScale.x;

            float angularDiffLeft = 0f;
            for (int i = 0; newLeftRotations != null && lastLeftRotations != null && i < newLeftRotations.Count && i < lastLeftRotations.Count; i++) {
                rawLeftRotations.Add(newLeftRotations[i]);
                if (lastRawLeftRotations != null && rawLeftRotations != null && lastRawLeftRotations.Count == rawLeftRotations.Count)
                    angularDiffLeft += Mathf.Abs(Quaternion.Angle(lastRawLeftRotations[i], rawLeftRotations[i]));
                newLeftRotations[i] = Quaternion.Lerp(lastLeftRotations[i], newLeftRotations[i], 1f - smoothing);
            }
            lastRawLeftRotations = rawLeftRotations;
            if (newLeftRotations != null)
            angularDiffLeft /= (float)newLeftRotations.Count;
            float leftDist = Vector3.Distance(LocalSpace(leftPalmPos) / transform.lossyScale.x, lastRawLeftPosition);
            if (angularDiffLeft > angularThreshold && (leftAliveCount == 1 || (distanceThresholdLow < leftDist && leftDist < distanceThresholdHigh)))
                leftBad = Math.Min(maxBadness, leftBad + badFactor + Math.Max(0, 6 - leftAliveCount));
            else
                leftBad = Math.Max(0, leftBad - goodFactor);
            if (rangeLimit && leftDist > jumpThreshold && leftAliveCount > 2) {
                leftBad = Math.Min(maxBadness, leftBad + badThreshold);
            }
            if (rangeLimit && leftAliveCount == 1 &&Vector3.Distance(LocalSpace(leftPalmPos) / transform.lossyScale.x, Vector3.zero) > maximumRange) {
                leftAliveCount = 0;
                leftActive = false;
            }
            lastRawLeftPosition = LocalSpace(leftPalmPos) / transform.lossyScale.x;

            bool doMirror = (applyMirror && !swap) || (swap && !applyMirror);
            
            if (lastMirror != doMirror) {
                Vector3 tmpV = lastRightPos;
                lastRightPos = lastLeftPos;
                lastLeftPos = tmpV;
                Quaternion tmpQ = lastRightRot;
                lastRightRot = lastLeftRot;
                lastLeftRot = tmpQ;
            }
            
            bool lastGotLeft = gotLeft;
            bool lastGotRight = gotRight;
            gotLeft = false;
            gotRight = false;
            
            lastLeftPos = leftPos;
            lastLeftRot = leftRot;
            lastRightPos = rightPos;
            lastRightRot = rightRot;
            
            if (doMirror) {
                Quaternion inverse = Quaternion.Inverse(transform.rotation);
                if (rightActive && rightHand != null) {
                    leftPos = GlobalSpace(transform.rotation * MirrorTranslation(inverse * LocalSpace(rightPalmPos)));
                    leftRot = rightHand.GetPalmRotation() * r.Reorientation() * Quaternion.AngleAxis(180f, Vector3.up);
                    leftRot = transform.rotation * MirrorQuaternion(inverse * leftRot);
                    gotLeft = true;
                    if (rightBad > badThreshold || (lastGotLeft != gotLeft && rightBad > 0))
                        gotLeft = false;
                }
                if (leftActive && leftHand != null) {
                    rightPos = GlobalSpace(transform.rotation * MirrorTranslation(inverse * LocalSpace(leftPalmPos)));
                    rightRot = leftHand.GetPalmRotation() * l.Reorientation() * Quaternion.AngleAxis(180f, Vector3.up);
                    rightRot = transform.rotation * MirrorQuaternion(inverse * rightRot);
                    gotRight = true;
                    if (leftBad > badThreshold || (lastGotRight != gotRight && leftBad > 0))
                        gotRight = false;
                }
            } else {
                if (leftActive && leftHand != null) {
                    leftPos = leftPalmPos;
                    leftRot = leftHand.GetPalmRotation() * l.Reorientation() * Quaternion.AngleAxis(180f, Vector3.up);
                    gotLeft = true;
                    if (leftBad > badThreshold || (lastGotLeft != gotLeft && leftBad > 0))
                        gotLeft = false;
                }
                if (rightActive && rightHand != null) {
                    rightPos = rightPalmPos;
                    rightRot = rightHand.GetPalmRotation() * r.Reorientation() * Quaternion.AngleAxis(180f, Vector3.up);
                    gotRight = true;
                    if (rightBad > badThreshold || (lastGotRight != gotRight && rightBad > 0))
                        gotRight = false;
                }
            }

            if (lastGotLeft != gotLeft && gotLeft) {
                firstGotLeft = Time.time;
                lastLeftPos = leftPos;
                lastLeftRot = leftRot;
            }
            
            if (lastGotRight != gotRight && gotRight) {
                firstGotRight = Time.time;
                lastRightPos = rightPos;
                lastRightRot = rightRot;
            }
            
            if ((gotLeft || gotRight) && interpolateState < 2)
                interpolateState++;
            if (interpolateState > 1)
                avgInterps = Mathf.Lerp(avgInterps, (float)interpolationCount, 0.15f);
            interpolationCount = 0;
            
            leftPos = Vector3.Lerp(lastLeftPos, leftPos, 1f - smoothing);
            leftRot = Quaternion.Lerp(lastLeftRot, leftRot, 1f - smoothing);
            lastGotLeft = gotLeft;

            rightPos = Vector3.Lerp(lastRightPos, rightPos, 1f - smoothing);
            rightRot = Quaternion.Lerp(lastRightRot, rightRot, 1f - smoothing);
            lastGotRight = gotRight;

            lastMirror = doMirror;
            
            Interpolate();
            
            transform.position = preReferencePos;
            transform.rotation = preReferenceRot;
        }
    }
    
    void Interpolate() {
        if (!track) {
            gotLeft = false;
            gotRight = false;
        }
        
        float t = Mathf.Clamp((float)interpolationCount / avgInterps, 0f, 0.985f);
        float now = Time.time;
        interpolationCount++;
        if (gotLeft) {
            float dt = Mathf.Clamp((now - firstGotLeft) / lerpPeriodIn + (1f - dtLeftOut), 0f, 1f);
            dtLeftIn = dt;
            if (skip <= 0) {
                vrIK.solver.leftArm.IKPosition = Vector3.Lerp(lastLeftPos, leftPos, t);
                vrIK.solver.leftArm.IKRotation = Quaternion.Lerp(lastLeftRot, leftRot, t);
                vrIK.solver.leftArm.positionWeight = dt;
                vrIK.solver.leftArm.rotationWeight = dt;
            }
            lastLeft = now;
        } else {
            float dt = Mathf.Clamp(((now - lastLeft) - gracePeriod) / lerpPeriodOut - (1f - dtLeftIn), 0f, 1f);
            dtLeftOut = dt;
            if (skip <= 0) {
                vrIK.solver.leftArm.positionWeight = Mathf.Lerp(1f, 0f, dt);
                vrIK.solver.leftArm.rotationWeight = Mathf.Lerp(1f, 0f, dt);
            }
            FadeJointRotations(l, lastGoodLeftRotations, dt);
        }

        if (gotRight) {
            float dt = Mathf.Clamp((now - firstGotRight) / lerpPeriodIn + (1f - dtRightOut), 0f, 1f);
            dtRightIn = dt;
            if (skip <= 0) {
                vrIK.solver.rightArm.IKPosition = Vector3.Lerp(lastRightPos, rightPos, t);
                vrIK.solver.rightArm.IKRotation = Quaternion.Lerp(lastRightRot, rightRot, t);
                vrIK.solver.rightArm.positionWeight = dt;
                vrIK.solver.rightArm.rotationWeight = dt;
            }
            lastRight = now;
        } else {
            float dt = Mathf.Clamp(((now - lastRight) - gracePeriod) / lerpPeriodOut - (1f - dtRightIn), 0f, 1f);
            dtRightOut = dt;
            if (skip <= 0) {
                vrIK.solver.rightArm.positionWeight = Mathf.Lerp(1f, 0f, dt);
                vrIK.solver.rightArm.rotationWeight = Mathf.Lerp(1f, 0f, dt);
            }
            FadeJointRotations(r, lastGoodRightRotations, dt);
        }
        
        bool doMirror = (applyMirror && !swap) || (swap && !applyMirror);
        if (skip <= 0) {
            if (doMirror) {
                if (gotLeft)
                    SetJointRotations(l, lastRightRotations, newRightRotations, t, true);
                if (gotRight)
                    SetJointRotations(r, lastLeftRotations, newLeftRotations, t, true);
            }
            else {
                if (gotLeft)
                    SetJointRotations(l, lastLeftRotations, newLeftRotations, t, false);
                if (gotRight)
                    SetJointRotations(r, lastRightRotations, newRightRotations, t, false);
            }
        } else {
            skip--;
        }
        lastGoodLeftRotations = GetJointRotations(l);
        lastGoodRightRotations = GetJointRotations(r);
    }


    Hand UpdateHand (HandModel hand, Leap.Hand curHand) {
        Vector3[] pos = new Vector3[4];
        Quaternion[] rot = new Quaternion[4];
        
        if (hand.palm != null) {
            pos[0] = hand.palm.localPosition;
            rot[0] = hand.palm.localRotation;
        }
        if (hand.forearm != null) {
            pos[1] = hand.forearm.localPosition;
            rot[1] = hand.forearm.localRotation;
        }
        if (hand.wristJoint != null) {
            pos[2] = hand.wristJoint.localPosition;
            rot[2] = hand.wristJoint.localRotation;
        }
        if (hand.elbowJoint != null) {
            pos[3] = hand.elbowJoint.localPosition;
            rot[3] = hand.elbowJoint.localRotation;
        }
        
        hand.SetLeapHand(curHand);
        hand.UpdateHand();
        Hand leapHand = hand.GetLeapHand();
        
        if (hand.palm != null) {
            hand.palm.localPosition = pos[0];
            hand.palm.localRotation = rot[0];
        }
        if (hand.forearm != null) {
            hand.forearm.localPosition = pos[1];
            hand.forearm.localRotation = rot[1];
        }
        if (hand.wristJoint != null) {
            hand.wristJoint.localPosition = pos[2];
            hand.wristJoint.localRotation = rot[2];
        }
        if (hand.elbowJoint != null) {
            hand.elbowJoint.localPosition = pos[3];
            hand.elbowJoint.localRotation = rot[3];
        }
        
        return leapHand;
    }

    /// <summary>
    /// Tells the hands to update to match the new Leap Motion hand frame data. Also keeps track of
    /// which hands are currently active.
    /// </summary>
    void UpdateHandRepresentations() {
        if (failed)
            return;
        (leapProvider as LeapServiceProvider).RetransformFrames();
        leftActive = false;
        rightActive = false;
        foreach (Leap.Hand curHand in leapProvider.CurrentFrame.Hands) {
            if (curHand.IsLeft && l != null) {
                Hand hand = UpdateHand(leftHand, curHand);
                leftActive = true;
                leftAliveCount++;
            }
            if (curHand.IsRight && r != null) {
                Hand hand = UpdateHand(rightHand, curHand);
                rightActive = true;
                rightAliveCount++;
            }
        }
        if (!leftActive) {
            lastRawLeftRotations = null;
            leftAliveCount = 0;
            leftBad = 0;
        }
        int minCount = 4;
        if (leftAliveCount < minCount)
            leftActive = false;
        if (!rightActive) {
            lastRawRightRotations = null;
            rightAliveCount = 0;
            rightBad = 0;
        }
        if (rightAliveCount < minCount)
            rightActive = false;
        if (!leftActive && !rightActive) {
            interpolateState = 0;
            interpolationCount = 1;
            avgInterps = 1f;
        }
    }
}