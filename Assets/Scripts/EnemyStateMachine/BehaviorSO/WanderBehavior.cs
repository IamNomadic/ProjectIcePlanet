using System;
using System.Collections;
using UnityEngine;
[CreateAssetMenu(menuName = "Enemy/States/wander", fileName = "wanderBehaviour")]
public class WanderBehaviour : EnemyStateBehaviour
{
    [Header("Wander Settings")]
    [Tooltip("Maximum distance from the origin (set when Enter runs) to pick wander targets")]
    public float wanderRadius = 5f;
    [Tooltip("Movement speed while wandering (units/sec)")]
    public float moveSpeed = 2.5f;
    [Tooltip("How often to pick a new wander target (seconds)")]
    public float pickInterval = 2.5f;
    [Tooltip("How close to the target before considering it reached")]
    public float arriveThreshold = 0.25f;
    [Tooltip("If set true, wander keeps the initial Y and only moves horizontally")]
    public bool keepInitialY = true;

    // runtime
    private Vector3 origin;
    private Vector3 targetPos;
    private float nextPickTime;
    private Transform agentTransform;
    private Rigidbody agentRb;
    private object ctxObject; // raw context reference (kept for reflection fallback)

    public override void Enter(StateMachineContext ctx)
    {
        ctxObject = ctx;

        // try to get Transform and Rigidbody from the context by common names/reflection
        agentTransform = TryGetTransformFromContext(ctx);
        agentRb = TryGetRigidbodyFromContext(ctx);

        // fallback: try every component on ctx if it's a GameObject
        if (agentTransform == null)
        {
            var go = TryGetGameObjectFromContext(ctx);
            if (go != null) agentTransform = go.transform;
        }

        if (agentTransform == null)
        {
            Debug.LogWarning("WanderBehaviour: could not find a Transform in the provided StateMachineContext. Behaviour will not move anything.");
            origin = Vector3.zero;
        }
        else
        {
            origin = agentTransform.position;
            if (keepInitialY) origin.y = agentTransform.position.y;
        }

        PickNewTarget();
        nextPickTime = Time.time + pickInterval;
    }

    public override void Execute(StateMachineContext ctx)
    {
        if (agentTransform == null)
        {
            // try again once per Execute in case context changed
            agentTransform = TryGetTransformFromContext(ctx);
            if (agentTransform == null)
                return;
        }

        // pick a new target periodically
        if (Time.time >= nextPickTime)
        {
            PickNewTarget();
            nextPickTime = Time.time + pickInterval;
        }

        Vector3 pos = agentTransform.position;
        Vector3 toTarget = targetPos - pos;

        // if keeping Y, ensure movement only horizontally
        if (keepInitialY)
            toTarget.y = 0f;

        float dist = toTarget.magnitude;

        if (dist <= arriveThreshold)
        {
            // reached - pick a new target immediately
            PickNewTarget();
            nextPickTime = Time.time + pickInterval;
            toTarget = targetPos - agentTransform.position;
            if (keepInitialY) toTarget.y = 0f;
            dist = toTarget.magnitude;
            if (dist <= Mathf.Epsilon) return;
        }

        Vector3 desiredVel = toTarget.normalized * moveSpeed;

        if (agentRb == null)
        {
            // try to obtain Rigidbody if it became available
            agentRb = TryGetRigidbodyFromContext(ctx);
        }

        if (agentRb != null)
        {
            // preserve vertical velocity to avoid interfering with hover or jumps
            Vector3 newVel = new Vector3(desiredVel.x, agentRb.velocity.y, desiredVel.z);
            agentRb.velocity = newVel;
        }
        else
        {
            // move the transform directly (frame-rate independent)
            agentTransform.position += desiredVel * Time.deltaTime;
        }
    }

    public override void Exit(StateMachineContext ctx)
    {
        // Stop motion when leaving
        if (agentRb != null)
        {
            Vector3 v = agentRb.velocity;
            agentRb.velocity = new Vector3(0f, v.y, 0f);
        }
    }

    // ----- helpers -----

    private void PickNewTarget()
    {
        // choose a point in a circle around origin
        float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        float radius = UnityEngine.Random.Range(0f, wanderRadius);
        Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
        targetPos = origin + offset;

        if (keepInitialY && agentTransform != null)
            targetPos.y = agentTransform.position.y;
    }

    // Try to extract a Transform from the context using common property/field names
    private Transform TryGetTransformFromContext(object ctx)
    {
        if (ctx == null) return null;

        // if context itself has a transform property (likely)
        var ctxType = ctx.GetType();

        // common direct properties
        string[] transformNames = { "transform", "Transform", "agentTransform", "ownerTransform", "owner", "agent" };

        foreach (var name in transformNames)
        {
            var prop = ctxType.GetProperty(name);
            if (prop != null)
            {
                var val = prop.GetValue(ctx);
                if (val is Transform t) return t;
                if (val is GameObject go) return go.transform;
                if (val is Component c) return c.transform;
            }

            var fld = ctxType.GetField(name);
            if (fld != null)
            {
                var val = fld.GetValue(ctx);
                if (val is Transform t) return t;
                if (val is GameObject go) return go.transform;
                if (val is Component c) return c.transform;
            }
        }

        // if the context is itself a Component or GameObject
        if (ctx is Component comp) return comp.transform;
        if (ctx is GameObject gobj) return gobj.transform;

        // try property "gameObject"
        var goProp = ctxType.GetProperty("gameObject");
        if (goProp != null)
        {
            var go = goProp.GetValue(ctx) as GameObject;
            if (go != null) return go.transform;
        }

        return null;
    }

    // Try to extract a Rigidbody from the context using common names or the transform/gameobject
    private Rigidbody TryGetRigidbodyFromContext(object ctx)
    {
        if (ctx == null) return null;

        var ctxType = ctx.GetType();
        string[] rbNames = { "rb", "rigidbody", "rigidBody", "agentRigidbody", "rigidbodyComponent" };

        foreach (var name in rbNames)
        {
            var prop = ctxType.GetProperty(name);
            if (prop != null)
            {
                var val = prop.GetValue(ctx);
                if (val is Rigidbody r) return r;
            }

            var fld = ctxType.GetField(name);
            if (fld != null)
            {
                var val = fld.GetValue(ctx);
                if (val is Rigidbody r) return r;
            }
        }

        // try get Rigidbody from transform or gameObject
        var t = TryGetTransformFromContext(ctx);
        if (t != null)
        {
            var rb = t.GetComponent<Rigidbody>();
            if (rb != null) return rb;
        }

        var go = TryGetGameObjectFromContext(ctx);
        if (go != null)
        {
            var rb = go.GetComponent<Rigidbody>();
            if (rb != null) return rb;
        }

        return null;
    }

    private GameObject TryGetGameObjectFromContext(object ctx)
    {
        if (ctx == null) return null;

        var ctxType = ctx.GetType();

        var goProp = ctxType.GetProperty("gameObject");
        if (goProp != null)
        {
            var go = goProp.GetValue(ctx) as GameObject;
            if (go != null) return go;
        }

        var ownerProp = ctxType.GetProperty("owner");
        if (ownerProp != null)
        {
            var ownerVal = ownerProp.GetValue(ctx);
            if (ownerVal is GameObject go2) return go2;
            if (ownerVal is Component comp) return comp.gameObject;
        }

        if (ctx is GameObject gobj) return gobj;
        if (ctx is Component comp2) return comp2.gameObject;

        return null;
    }
}
