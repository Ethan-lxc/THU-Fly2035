using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IsoDroneController : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float smoothTime = 0.2f;
    public float hoverAmplitude = 0.15f;
    public float hoverFrequency = 2.5f;

    private Vector2 targetWorldPos;
    private Vector2 waypoint;
    private Vector2 currentVelocity;
    private bool isMovingToWaypoint = false;
    private bool isMovingToTarget = false;

    private Transform visualChild;
    private SpriteRenderer spriteRenderer;

    void Awake()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (transform.childCount > 0) visualChild = transform.GetChild(0);
        targetWorldPos = transform.position;
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            CalculateIsoPath();
        }
        ApplyHover();
    }

    void CalculateIsoPath()
    {
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        targetWorldPos = new Vector2(mousePos.x, mousePos.y);

        Vector2 delta = targetWorldPos - (Vector2)transform.position;

        // 轴测分解
        float b = (delta.y - 0.5f * delta.x);
        float a = delta.x + b;

        waypoint = (Vector2)transform.position + new Vector2(a, a * 0.5f);

        isMovingToWaypoint = true;
        isMovingToTarget = true;
    }

    void FixedUpdate()
    {
        if (!isMovingToTarget) return;

        if (isMovingToWaypoint)
        {
            // --- 拐点阶段：使用常量速度，防止减速 ---
            Vector2 dir = (waypoint - (Vector2)transform.position).normalized;
            // 模拟平滑启动，但接近拐点时不减速
            currentVelocity = Vector2.Lerp(currentVelocity, dir * moveSpeed, 0.1f);
            transform.position += (Vector3)currentVelocity * Time.fixedDeltaTime;

            // 到达拐点判定（距离够近就直接切换，不等待减速）
            if (Vector2.Distance(transform.position, waypoint) < 0.2f)
            {
                isMovingToWaypoint = false;
            }
        }
        else
        {
            // --- 终点阶段：使用 SmoothDamp 实现自然减速停止 ---
            transform.position = Vector2.SmoothDamp(
                transform.position, 
                targetWorldPos, 
                ref currentVelocity, 
                smoothTime, 
                moveSpeed
            );

            if (Vector2.Distance(transform.position, targetWorldPos) < 0.05f)
            {
                isMovingToTarget = false;
                currentVelocity = Vector2.zero;
            }
        }

        // 转向处理
        if (currentVelocity.x != 0) spriteRenderer.flipX = currentVelocity.x < 0;
    }

    void ApplyHover()
    {
        if (visualChild != null)
        {
            float yOffset = Mathf.Sin(Time.time * hoverFrequency) * hoverAmplitude;
            visualChild.localPosition = new Vector3(0, yOffset, 0);
        }
    }
}