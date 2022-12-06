﻿using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using ElRaccoone.Tweens;
using UnityEngine;

namespace UI {
    public class CardContainer : MonoBehaviour {
        private List<GameObject> sprites = new();
        [SerializeField] private Texture2D texture;

        [SerializeField] private float RotationPerCard = 10f;

        public delegate void CardEvent(CardData data, GameObject target);

        public event CardEvent onCardPlayed;
        
        private readonly DragManager dragManager = new();
        private readonly RaycastHit2D[] raycastResults = new RaycastHit2D[16];
        private ContactFilter2D contactFilter;
        private Vector2 cardVelocity = Vector2.zero;
        private GameObject lastHoveredCard;
        private GameObject secondaryHoveredCard;

        public CardContainer() {
            contactFilter.useLayerMask = true;
            dragManager.onDragPerformed += (draggedCard, position, stage) => {
                var newPos = Vector2.SmoothDamp(draggedCard.transform.position, position, ref cardVelocity, 0.1f);
                draggedCard.transform.position = newPos;
                if (stage == DragManager.DragStage.Done) {
                    var target = FindObjectUnderMouse(LayerMask.GetMask("Enemy", "Player", "Card"), o => o != draggedCard);
                    if (target != null && target.layer == LayerMask.NameToLayer("Card")) {
                        var newIndex = sprites.IndexOf(target);
                        sprites.Remove(draggedCard);
                        sprites.Insert(newIndex, draggedCard);
                    } else {
                        lastHoveredCard = null;
                        var usable = true;
                        // var usable = draggedCard.isUsableOn(target);
                        if (usable) {
                            // onCardPlayed(draggedCard.cardData, target);
                            RemoveCard(draggedCard);
                        }
                    }

                    AlignCards();
                }

                if (stage == DragManager.DragStage.Begin) {
                    draggedCard.GetComponent<SpriteRenderer>().sortingOrder = 100;
                    draggedCard.TweenRotation(Vector3.zero, 0.3f);
                } 
            };
        }

        private void RemoveCard(GameObject draggedObject) {
            draggedObject.TweenLocalScale(Vector3.zero, 0.3f);
            draggedObject.transform.parent = null;
            sprites.Remove(draggedObject);
            Destroy(draggedObject, 1);
        }

        public void AddCard() {
            AddNewCard();
            AlignCards(true);
        }

        private GameObject FindObjectUnderMouse(int layerMask, Func<GameObject, bool> filter = null) {
            contactFilter.layerMask = layerMask;
            var size = Physics2D.Raycast(GetMousePosition(), Vector2.left, contactFilter, raycastResults, 0.01f);
            if (size > 0) {
                return raycastResults
                    .Take(size)
                    .Select(a => a.collider.gameObject)
                    .Where(filter ?? (_ => true))
                    .OrderByDescending(c => c.transform.position.z)
                    .FirstOrDefault();
            }

            return null;
        }

        private Vector2 GetMousePosition() => Camera.main.ScreenToWorldPoint(Input.mousePosition);

        private void OnMouseDown() {
            var card = FindObjectUnderMouse(LayerMask.GetMask("Card"));
            if (card != null) {
                dragManager.BeginDrag(card, GetMousePosition());
            }
        }

        private void OnMouseUp() {
            dragManager.EndDrag(GetMousePosition());
        }

        private void OnMouseOver() {
            if (!dragManager.isDragging) {
                var card = FindObjectUnderMouse(LayerMask.GetMask("Card"));
                if (lastHoveredCard != null && lastHoveredCard != card) {
                    lastHoveredCard.TweenLocalScale(Vector3.one, 0.2f);
                }
                if (card != null) {
                    card.TweenLocalScale(Vector3.one * 1.3f, 0.2f);
                }

                lastHoveredCard = card;
            } else {
                var card = FindObjectUnderMouse(LayerMask.GetMask("Card"), c => c != dragManager.draggedObject);
                if (secondaryHoveredCard != null && secondaryHoveredCard != card) {
                    secondaryHoveredCard.TweenLocalScale(Vector3.one, 0.2f);
                }

                if (card != null) {
                    card.TweenLocalScale(Vector3.one * 1.15f, 0.2f);
                }

                secondaryHoveredCard = card;
            }
        }

        private void OnMouseExit() {
            if (lastHoveredCard != null && !dragManager.isDragging) {
                lastHoveredCard.TweenLocalScale(Vector3.one, 0.2f);
                lastHoveredCard = null;
            }

            if (secondaryHoveredCard != null) {
                secondaryHoveredCard.TweenLocalScale(Vector3.one, 0.2f);
                secondaryHoveredCard = null;
            }
        }

        private void Update() {
            dragManager.MoveMouse(GetMousePosition());
        }

        private void AddNewCard() {
            var go = new GameObject("Card");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 1080);
            sr.sortingOrder = sprites.Count;
            go.transform.parent = transform;
            go.AddComponent<BoxCollider2D>();
            go.layer = LayerMask.NameToLayer("Card");
            sprites.Add(go);
        }

        // ReSharper disable Unity.PerformanceAnalysis
        private void AlignCards(bool newCard = false) {
            var size = (sprites.Count - 1);
            for (int i = 0; i < sprites.Count; ++i) {
                var sprite = sprites[i];
                
                if (sprite.gameObject == dragManager.draggedObject) continue;
                
                var sprTransform = sprite.transform;
                var sprCollider = sprite.GetComponent<BoxCollider2D>();
                var pivot = (sprites.Count - 1.0f) / 2.0f;
                var distance = i - pivot;
                var rotation = new Vector3(0, 0, -RotationPerCard * distance);
                var currentCardPosition = sprTransform.position;
                var offset = (Vector3) sprCollider.size / 2;
                var highPoint = rotation.z switch {
                    < 0 => currentCardPosition + offset,
                    > 0 => currentCardPosition + Vector3.Scale(offset, new Vector3(-1, 1, 1)),
                    _ => currentCardPosition + new Vector3(0, offset.y)
                };
                var rotatedHighPoint = RotatePointAroundPivot(highPoint, currentCardPosition, rotation);
                var yOffset = Math.Abs(currentCardPosition.y + offset.y - rotatedHighPoint.y);
                // LEAVE IT AS IS FOR NOW, I SPENT 2 WEEKS TRYING TO ADJUST IT AND IM CRYING
                var position = new Vector3((0.7f * i) - size / 2.0f, -yOffset * 2 /*+ (float) (-Math.Abs(rotation.z) * 0.015)*/, i);
                if (i == sprites.Count - 1 && newCard) {
                    sprTransform.eulerAngles = rotation;
                    sprTransform.localPosition = position;
                    sprTransform.localScale = Vector3.zero;
                    sprTransform.TweenLocalScale(Vector3.one, 0.3f);
                } else {
                    sprite.GetComponent<SpriteRenderer>().sortingOrder = i;
                    sprTransform.TweenRotation(rotation, 0.3f);
                    sprTransform.TweenLocalPosition(position, 0.3f);
                }
            }
        }

        private void OnDrawGizmos() {
            Gizmos.color = Color.blue;
            var pivot = (sprites.Count - 1.0f) / 2.0f;
            for(int i = 0; i < sprites.Count; ++i) {
                var sprite = sprites[i];
                var sprCollider = sprite.GetComponent<BoxCollider2D>();
                var distance = i - pivot;
                var rotation = new Vector3(0, 0, -RotationPerCard * distance);
                var currentCardPosition = sprite.transform.position;
                var offset = (Vector3) sprCollider.size / 2;
                var highPoint = rotation.z switch {
                    < 0 => currentCardPosition + offset,
                    > 0 => currentCardPosition + Vector3.Scale(offset, new Vector3(-1, 1, 1)),
                    _ => currentCardPosition + new Vector3(0, offset.y)
                };
                var rotatedHighPoint = RotatePointAroundPivot(highPoint, currentCardPosition, rotation);
                Gizmos.color = Color.blue;
                Gizmos.DrawSphere(rotatedHighPoint, 0.1f);
                Gizmos.color = Color.green;
                Gizmos.DrawSphere(highPoint, 0.1f);
            }
        }

        private void Awake() {
            KeepAddingCards().Forget();
        }

        private Vector3 RotatePointAroundPivot(Vector3 point, Vector3 pivot, Vector3 angles) {
            var dir = point - pivot;
            dir = Quaternion.Euler(angles) * dir;
            return dir + pivot;
        }

        private async UniTaskVoid KeepAddingCards() {
            for (int i = 0; i < 8; ++i) {
                await UniTask.Delay(TimeSpan.FromSeconds(1));
                AddCard();
            }
        }
    }
}