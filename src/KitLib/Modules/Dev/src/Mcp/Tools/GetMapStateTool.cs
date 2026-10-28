using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;
using KitLib.Game;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace KitLib.Mcp.Tools;

/// <summary>Exposes the whole act map so callers can plan routes across rows.
/// 返回整幕地图，供跨行路线规划。</summary>
internal sealed class GetMapStateTool : IMcpTool {
    public string Name => "get_map_state";
    public string Description =>
        "Get the full act map (every node's row/col/pointType plus parents and children), so routes can be " +
        "planned across rows. available lists the currently selectable nodes; its index is what " +
        "map_action select_map_node expects. Available is only filled while the map screen is open.";
    public string InputSchemaJson => """
    {
        "type": "object",
        "properties": {},
        "required": []
    }
    """;

    public Task<JsonNode> ExecuteAsync(JsonObject args) {
        if (!RunContext.TryGetRunAndPlayer(out var state, out _) || state.Map is not { } map) {
            return Task.FromResult<JsonNode>(new JsonObject {
                ["ok"] = false,
                ["error"] = "No active run or map.",
            });
        }

        var points = map.GetAllMapPoints().ToList();
        // GetAllMapPoints leaves out the boss row, but route planning needs its coordinate.
        if (map.BossMapPoint is { } boss && points.All(p => !p.coord.Equals(boss.coord)))
            points.Add(boss);

        var nodes = new JsonArray();
        foreach (var point in points.OrderBy(p => p.coord.row).ThenBy(p => p.coord.col)) {
            nodes.Add(new JsonObject {
                ["row"] = point.coord.row,
                ["col"] = point.coord.col,
                ["pointType"] = point.PointType.ToString(),
                ["parents"] = Coords(point.parents),
                ["children"] = Coords(point.Children),
            });
        }

        var available = new JsonArray();
        bool mapScreenOpen = NMapScreen.Instance is { IsOpen: true };
        if (mapScreenOpen) {
            var allPoints = GameUi.FindAll<NMapPoint>((Node)NMapScreen.Instance!);
            var selectable = LeanGameSnapshot.GetAvailableMapPoints(state, allPoints);
            for (int i = 0; i < selectable.Count; i++) {
                available.Add(new JsonObject {
                    ["index"] = i,
                    ["row"] = selectable[i].Point.coord.row,
                    ["col"] = selectable[i].Point.coord.col,
                    ["pointType"] = selectable[i].Point.PointType.ToString(),
                });
            }
        }

        return Task.FromResult<JsonNode>(new JsonObject {
            ["ok"] = true,
            ["act"] = state.CurrentActIndex,
            ["totalFloor"] = state.TotalFloor,
            ["current"] = state.CurrentMapCoord is { } current ? CoordObj(current) : null,
            ["mapScreenOpen"] = mapScreenOpen,
            ["available"] = available,
            ["nodes"] = nodes,
        });
    }

    private static JsonObject CoordObj(MapCoord coord) => new() {
        ["row"] = coord.row,
        ["col"] = coord.col,
    };

    private static JsonArray Coords(IEnumerable<MapPoint> points) {
        var arr = new JsonArray();
        foreach (var point in points)
            arr.Add(new JsonArray { point.coord.row, point.coord.col });
        return arr;
    }
}
