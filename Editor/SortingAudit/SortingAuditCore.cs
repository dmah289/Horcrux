using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Horcrux.Editor.SortingAudit
{
    public enum SortingRule
    {
        DeadLayer = 1,
        ChildDeclaresLayer = 2,
        OrderOutOfRange = 3,
        Root = 4,
    }

    public readonly struct SortingFinding
    {
        public readonly SortingRule Rule;
        public readonly Object Asset;
        public readonly GameObject Target;
        public readonly string Message;

        public SortingFinding(SortingRule rule, Object asset, GameObject target, string message)
        {
            Rule = rule;
            Asset = asset;
            Target = target;
            Message = message;
        }
    }

    /// <summary>Quét một cây object đã dựng: renderer, SortingGroup, SpriteMask. Không chạm asset.</summary>
    public static class SortingAuditCore
    {
        public const int MaxChildOrder = 99;

        public static void Scan(GameObject root, Object asset, List<SortingFinding> into)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                Check(renderer.gameObject, renderer.sortingLayerID, renderer.sortingOrder, renderer.GetType().Name, asset, into);

            foreach (var group in root.GetComponentsInChildren<SortingGroup>(true))
                Check(group.gameObject, group.sortingLayerID, group.sortingOrder, nameof(SortingGroup), asset, into, selfIsGroup: true);

            foreach (var mask in root.GetComponentsInChildren<SpriteMask>(true))
            {
                if (!mask.isCustomRangeActive) continue;
                if (!SortingLayer.IsValid(mask.frontSortingLayerID) || !SortingLayer.IsValid(mask.backSortingLayerID))
                    into.Add(new SortingFinding(SortingRule.DeadLayer, asset, mask.gameObject, "SpriteMask: dải trỏ sorting layer không có trong TagManager"));
            }
        }

        private static void Check(GameObject go, int layerId, int order, string kind, Object asset, List<SortingFinding> into, bool selfIsGroup = false)
        {
            if (!SortingLayer.IsValid(layerId))
            {
                into.Add(new SortingFinding(SortingRule.DeadLayer, asset, go, $"{kind}: sorting layer {layerId} không có trong TagManager, Unity vẽ ở Default"));
                return;
            }

            // GetComponentInParent trên chính object có group trả về nó, nên group hỏi từ parent để tìm group tổ tiên.
            var parentGroup = selfIsGroup
                ? (go.transform.parent != null ? go.transform.parent.GetComponentInParent<SortingGroup>(true) : null)
                : go.GetComponentInParent<SortingGroup>(true);

            if (parentGroup == null)
            {
                into.Add(new SortingFinding(SortingRule.Root, asset, go, $"{kind}: {SortingLayer.IDToName(layerId)} {order}"));
                return;
            }

            if (layerId != 0)
                into.Add(new SortingFinding(SortingRule.ChildDeclaresLayer, asset, go, $"{kind}: con của group '{parentGroup.name}' khai layer {SortingLayer.IDToName(layerId)}, group nuốt layer này"));

            if (order < 0 || order > MaxChildOrder)
                into.Add(new SortingFinding(SortingRule.OrderOutOfRange, asset, go, $"{kind}: order {order} ngoài 0…{MaxChildOrder} trong group '{parentGroup.name}'"));
        }
    }
}
