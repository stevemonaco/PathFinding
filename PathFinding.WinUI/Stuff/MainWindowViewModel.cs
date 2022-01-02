﻿using PathFinding.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using PathFinding.Annotations;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Point = Windows.Foundation.Point;

namespace PathFinding.Stuff;

public enum ClickMode { Player = 0, Conveyor = 1 }

public class MainWindowViewModel : ObservableObject
{
    private Random rand = new Random(1);
    public bool Paused { get; set; } = false;
    public string HoveredEntityDescription { get => _hoveredEntityDescription; set => SetProperty(ref _hoveredEntityDescription, value); }
    public State State { get; set; }
    public List<Tile> EntitiesToHighlight { get; set; } = new();
    public List<Tile> AlreadyClicked { get; } = new();
    public Cell[,] AnswerCells { get; set; }
    private int _fps;
    private string _hoveredEntityDescription;
    private string _tileString;
    private long _ms;
    public bool AllowDiagonal { get => _allowDiagonal; set => SetProperty(ref _allowDiagonal, value); }
    public bool AlwaysPath { get; set; } = true;
    public int TileSize { get; set; }
    public int Top { get; set; }
    public int Left { get; set; }
    public bool ShowNumbers { get; set; } = false;
    public int PixelWidth { get; set; }
    public int PixelHeight { get; set; }
    public bool LeftButtonClick { get; set; }
    public string TileString { get => _tileString; set => SetProperty(ref _tileString, value); }
    public int TileWidth { get; set; }
    public int TileHeight { get; set; }
    public int CellsScored { get => _cellsScored; set => SetProperty(ref _cellsScored, value); }
    public string NewTileWidth { get; set; }
    public string NewTileHeight { get; set; }
    public int Fps { get => _fps; set => SetProperty(ref _fps, value); }
    public IStatePersistence Sp;
    public DateTime LastRequestedPathFind { get; set; }
    private int _cellsScored;
    private int _currentPlayer = 1;
    public int CurrentPlayer { get => _currentPlayer; set => SetProperty(ref _currentPlayer, value); }
    public Dictionary<int, (Tile Source, Tile Destination)> PlayerDictionary = new();
    private bool _allowDiagonal = true;
    public int PlayerCount { get; set; }
    public ClickMode ClickMode { get; set; } = ClickMode.Player;
    public Tile SelectedConveyorTile { get; set; }
    public List<Conveyor> Conveyors { get; set; } = new();
    public List<Item> Items { get; set; } = new();
    private int _tickCounter = 0;
    public readonly int MaxCellNumber = 3;

    public RelayCommand ResetCommand { get; set; }
    public AsyncRelayCommand<bool> ChangeDiagonalCommand { get; }

    public MainWindowViewModel(/*IStatePersistence statePersistence*/)
    {
        PlayerCount = 3;

        for (var i = 1; i <= PlayerCount; i++) { PlayerDictionary.Add(i, (null, null)); }

        PixelWidth = 600;
        PixelHeight = 600;
        TileWidth = 20;
        TileHeight = 20;
        var dpi = 96;
        var tileSize = 10;
        TileSize = tileSize;
        Sp = new StatePersistence();
        State = new(TileWidth, TileHeight, tileSize, Sp);
        
        ResetCommand = new(Reset);
        ChangeDiagonalCommand = new(async (x) =>
        {
            AllowDiagonal = x;
            await PlayerPathFinding();
        });

        SetupMapString();
        UploadMapString(@"3972_G4MPKI6UrMG9OgaGZmcAmLpKbFnWit9TRq0AVogGrRYWkQKSyyR9MuH9AenBv5sSeR+axQ4OHRwOESlSJAS5Z9t9tbIp5bwH/db7lgtKvQ3fH/yDCwonLSau8Y5aty1SCrbmxv20+I7bysl9AkepUN0bb8SGsANKLOiJCCOVU9u/PdYBet5gBsp6XKAeo8WuNkoNM9i8x35DUGYmR2HshCll3M4bPrVsPmGmOrfbSlJsVi5AcEMLzgbP00rTOd1HKeRUSX4C8L9Z9J5BfOKtSR8zs44M8O4CnJ34LyKDi1+JtWLy3HcqERkmHi4KuYeEAVZOkn7jlh3Ids0oomZVmhr0uiun2U/QT+4nJNkSyHAzQT3YYiIzDiVYB7yJesuCYk2e3Df2H0LT9zIwdRrIDAGbymDcWdFEvewh74DIswbs9HJiTy4feNxBJYXpdsockTBZH82r/DHk/7rdEm5CteuHA4xBLV4z55I+3SfLdkRAOvlY8Pk2kj0A");
    }

    public void Reset()
    {
        if (int.TryParse(NewTileWidth, out var tileWidth) && tileWidth > 10 && int.TryParse(NewTileHeight, out var tileHeight) && tileHeight > 10)
        {
            TileWidth = tileWidth;
            TileHeight = tileHeight;
            TileSize = Math.Max(Math.Min(PixelWidth / tileWidth, PixelHeight / tileHeight), 3);
            State = new(TileWidth, TileHeight, TileSize, Sp);
            AnswerCells = null;
            PlayerDictionary.Clear();
            for (var i = 1; i <= PlayerCount; i++) { PlayerDictionary.Add(i, (null, null)); }
        }
    }

    public void UploadMapString(string mapString)
    {
        var mapStringResult = State.UploadMapString(mapString);
        if (!mapStringResult.Success) return;

        var lineNumber = 1;
        State = new(mapStringResult.X, mapStringResult.Y, State.TileSize, Sp);
        for (var x = 0; x < mapStringResult.X; x++)
        {
            lineNumber++;
            var row = mapStringResult.mapStringArray[lineNumber];
            for (var y = 0; y < mapStringResult.Y; y++)
            {
                State.TileGrid[x, y].IsPassable = row[y] == '0';
            }
        }

        TileWidth = State.X;
        TileHeight = State.Y;
    }

    public void SetupMapString() => TileString = State.SetupMapString();

    public void GetHoverElements(Point? point)
    {
        if (!point.HasValue) return;
        var tileR = GetTileAtLocation(point);
        if (tileR is null) return;
        if (tileR == EntitiesToHighlight.FirstOrDefault()) return;
        HoveredEntityDescription = $"{tileR.X},{tileR.Y}";
        EntitiesToHighlight.Clear();
        EntitiesToHighlight.Add(tileR);
    }

    public async Task<(List<Cell> SolutionCells, Cell[,] AllCells, long TimeToSolve, DateTime thisDate)> PathFinding(Tile destination, Tile source, bool allowDiagonal, DateTime requestDate)
    {
        Cell destCell = new() { Id = destination.Id, HScore = 0, X = destination.X, Y = destination.Y, Passable = destination.IsPassable };
        Cell sourceCell = new() { Id = source.Id, HScore = Math.Abs(source.X - destination.X) + Math.Abs(source.Y - destination.Y), X = source.X, Y = source.Y, Passable = source.IsPassable };

        var cells = new Cell[State.X, State.Y];

        foreach (var t in State.Tiles)
        {
            var hScore = Math.Abs(t.X - destCell.X) + Math.Abs(t.Y - destCell.Y);
            var cell = new Cell()
            {
                HScore = hScore * 10,
                GScore = int.MaxValue / 2,
                X = t.X,
                Y = t.Y,
                Id = t.Id,
                Passable = t.IsPassable
            };
            if (t == destination) { destCell = cell; }
            if (t == source) { sourceCell = cell; }

            cells[t.X, t.Y] = cell;
        }

        //Not really useful at the moment.
        //await Chunking(cells, requestDate);
        var solution = await Solver.SolveAsync(cells, sourceCell, destCell, null, requestDate, allowDiagonal);
        if (solution.SolutionCells is null || !solution.SolutionCells.Any()) return default;
        Trace.WriteLine($"{solution.TimeToSolve}ms to solve ({sourceCell.X},{sourceCell.Y}) to ({destCell.X},{destCell.Y}).");
        return solution;
    }


    public async Task PlayerPathFinding()
    {
        var requestDate = DateTime.Now;
        LastRequestedPathFind = requestDate;

        foreach (var tile in State.TileGrid) { tile.IsPartOfSolution = false; }

        var keys = PlayerDictionary.Keys.ToArray();
        var notableCount = 0;
        for (var index = 0; index < keys.Length; index++)
        {
            var key = keys[index];
            var (source, destination) = PlayerDictionary[key];

            if (destination is null || source is null) { continue; }

            var solution = await PathFinding(destination, source, AllowDiagonal, requestDate);

            if (solution.SolutionCells is null || !solution.SolutionCells.Any() || solution.thisDate != LastRequestedPathFind) continue;

            Trace.WriteLine($"{solution.TimeToSolve}ms to solve ({source.X},{source.Y}) to ({source.X},{destination.Y}).");

            foreach (var cell in solution.SolutionCells) { State.TileGrid[cell.X, cell.Y].IsPartOfSolution = true; }
            foreach (var cell in solution.AllCells) { notableCount = (cell.FCost > int.MaxValue / 3) ? notableCount + 1 : notableCount; }

            //CellsScored = notableCount; //Wrong at the moment
            //AnswerCells = solution.AllCells; //Wrong at the moment
        }
    }

    [UsedImplicitly]
    private async Task Chunking(Cell[,] cells, DateTime thisDate)
    {
        var chunkSize = 8;
        var superCells = new List<Cell[,]>();
        var xChunks = TileWidth / chunkSize + 1;
        var yChunks = TileHeight / chunkSize + 1;
        //Just throw every tile into a chunk according to location

        SetChunksByGeometry(xChunks, yChunks, chunkSize, cells, superCells, out var chunkId);
        //Let's make sure that all of the cells in each chunk can reach all of the cells.

        await InvalidateDisconnectedCellsInChunks(superCells, chunkSize, thisDate);

        //let's go through all of the Chunk== -1 ones 

        foreach (var cell in cells)
        {
            if (cell.ChunkId == -1)
            {
            }
        }

        var color = 0;
        foreach (var supercell in superCells)
        {
            color++;
            foreach (var cell in supercell)
            {
                if (cell is null || State.TileGrid[cell.X, cell.Y] is null) continue;
                State.TileGrid[cell.X, cell.Y].ChunkId = cell.ChunkId;
            }
        }
    }

    private async Task InvalidateDisconnectedCellsInChunks(List<Cell[,]> superCells, int chunkSize, DateTime thisDate)
    {
        foreach (var supercell in superCells)
        {
            var tempCellGrid = new Cell[chunkSize + 1, chunkSize + 1];
            var initialCell = supercell[0, 0];
            for (var a = 0; a < chunkSize; a++)
            {
                for (var b = 0; b < chunkSize; b++)
                {
                    if (supercell[a, b] is null) continue;
                    tempCellGrid[a, b] = new()
                    {
                        Finished = false,
                        GScore = supercell[a, b].GScore,
                        Id = supercell[a, b].Id,
                        X = supercell[a, b].X % chunkSize,
                        Y = supercell[a, b].Y % chunkSize,
                        HScore = supercell[a, b].HScore,
                        Passable = supercell[a, b].Passable,
                        Destinations = new() { supercell[a, b] }
                    };
                }
            }

            Cell tempInitial = null;
            for (var a = 0; a < chunkSize; a++) //Need to find smallest index cell that is passable
            {
                for (var b = 0; b < chunkSize; b++)
                {
                    if (supercell[a, b] is null || !supercell[a, b].Passable) continue;
                    initialCell = tempCellGrid[a, b];
                    tempInitial = initialCell.Destinations.First();
                    //Trace.WriteLine($"Initial at: {tempInitial.X}, {tempInitial.Y}");
                    goto gitInitial;
                }
            }

        gitInitial:

            for (var a = 0; a < chunkSize; a++)
            {
                for (var b = 0; b < chunkSize; b++)
                {
                    if (tempCellGrid[a, b] is null || !tempCellGrid[a, b].Passable) continue;
                    var destinationCell = tempCellGrid[a, b];
                    if (initialCell.Id == destinationCell.Id) continue;
                    var result = await Solver.SolveAsync(tempCellGrid, initialCell, destinationCell, null, thisDate,
                        AllowDiagonal);
                    var resultCell = destinationCell.Destinations.First();

                    if (result.SolutionCells is null || !result.SolutionCells.Any())
                    {
                        /*Trace.WriteLine($"Could not navigate from {tempInitial.X}, {tempInitial.Y} to {resultCell.X}, {resultCell.Y}");*/
                        destinationCell.Destinations.First().ChunkId = -1;
                    }
                    else
                    {
                        /*Trace.WriteLine($"Navigated from {tempInitial.X}, {tempInitial.Y} to {resultCell.X}, {resultCell.Y} via :{string.Join(", ", result.SolutionCells.Select(c => $"({c.X},{c.Y})"))}");*/
                    }
                }
            }
        }
    }

    private void SetChunksByGeometry(int xChunks, int yChunks, int chunkSize, Cell[,] cells, List<Cell[,]> superCells, out int chunkId)
    {
        chunkId = -1;
        for (var x = 0; x < xChunks; x++)
        {
            for (var y = 0; y < yChunks; y++)
            {
                chunkId++;
                var tempChunk = new Cell[chunkSize + 1, chunkSize + 1];
                for (var a = 0; a <= chunkSize; a++)
                {
                    for (var b = 0; b <= chunkSize; b++)
                    {
                        if (x * chunkSize + a >= TileWidth || y * chunkSize + b >= TileHeight) { continue; }

                        tempChunk[a, b] = cells[x * chunkSize + a, y * chunkSize + b];
                        cells[x * chunkSize + a, y * chunkSize + b].ChunkId = chunkId;
                    }
                }
                superCells.Add(tempChunk);
            }
        }
    }

    public async Task HandleLeftClick(Point? point)
    {
        if (!point.HasValue) return;
        var tile = GetTileAtLocation(point);
        if (tile is null) return;
        switch (ClickMode)
        {
            case ClickMode.Player:
                await TryFlipElement(tile); return;
            case ClickMode.Conveyor:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private async Task TryFlipElement(Tile tile)
    {
        if (AlreadyClicked.Any(t => t.Id == tile.Id)) return;
        tile.IsPassable = !tile.IsPassable;
        if (!tile.IsPassable)
        {
            tile.TileRole = TileRole.Nothing;
            foreach (var kvp in PlayerDictionary)
            {
                if (kvp.Value.Destination == tile) PlayerDictionary[kvp.Key] = (kvp.Value.Source, null);
                if (kvp.Value.Source == tile) PlayerDictionary[kvp.Key] = (null, kvp.Value.Source);
            }
        }

        AlreadyClicked.Add(tile);
        EntitiesToHighlight.Clear();
        EntitiesToHighlight.Add(tile);
        if (AlwaysPath) await PlayerPathFinding();
        SetupMapString();
    }

    public Tile GetTileAtLocation(Point? point)
    {
        if (!point.HasValue) return null;
        var xThing = (int)Math.Floor((point.Value.X + Left) / TileSize);
        var yThing = (int)Math.Floor((point.Value.Y + Top) / TileSize);

        if (xThing >= TileWidth || yThing >= TileHeight) return null;

        return State.TileGrid[xThing, yThing];
    }

    public void FlipElementSourceDestination(Point? point)
    {
        //Stopwatch sw = new Stopwatch(); //2 ms
        //sw.Start();
        if (!point.HasValue) return;
        var tile = GetTileAtLocation(point);
        if (tile is null) return;
        switch (ClickMode)
        {
            case ClickMode.Player:
                HandleRightClickPlayerMode(tile);
                break;
            case ClickMode.Conveyor:
                HandleRightClickAddConveyorNode(tile);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private async void HandleRightClickAddConveyorNode(Tile tile)
    {
        //ToDo: I also need a way to make a conveyor land on another conveyor. 

        //ToDo: Here, I should make sure that it is either IsPassable or it lands on another conveyor.
        if (SelectedConveyorTile is null && tile.IsPassable) { SelectedConveyorTile = tile; return; }
        if (!tile.IsPassable) return;

        //ToDo: This is messed up
        var result = await PathFinding(SelectedConveyorTile, tile, false, DateTime.Now);
        SelectedConveyorTile = null;
        if (result.SolutionCells is null) return;
        var conveyor = new Conveyor() { Id = Conveyors.Count + 1 };
        //ToDo: I should go through this in reverse.
        for (var index = 0; index < result.SolutionCells.Count; index++)
        {
            var cell = result.SolutionCells[index];
            var nextCell = (index + 1 == result.SolutionCells.Count) ? null : result.SolutionCells[index + 1];
            var tempTile = State.TileGrid[result.SolutionCells[index].X, result.SolutionCells[index].Y];
            if (nextCell is null)
            {
                conveyor.ConveyorTile.Add(new() { Tile = tempTile, Direction = (0, 0) });
                continue;
            }

            var (x, y) = (cell.X - nextCell.X, cell.Y - nextCell.Y);
            tempTile.IsPassable = false;
            tempTile.TileRole = TileRole.Conveyor;
            conveyor.ConveyorTile.Add(new() { Tile = tempTile, Direction = (x, y), Conveyor = conveyor });
        }

        for (var index = 0; index < conveyor.ConveyorTile.Count; index++)
        {
            var conveyorTile = conveyor.ConveyorTile[index];
            var nextConveyorTile = (index + 1 == conveyor.ConveyorTile.Count) ? null : conveyor.ConveyorTile[index + 1];
            conveyorTile.NextConveyorTile = nextConveyorTile;
        }

        Conveyors.Add(conveyor);

        //ToDo: Separate this to another method. 
        for (var index = 0; index < Conveyors.Count; index++)
        {
            var conv = Conveyors[index];
            var tempTile = conv.ConveyorTile.Last();
            if (tempTile.NextConveyorTile is not null) continue;

            for (var i = 0; i < Conveyors.Count; i++)
            {
                var convMaster = Conveyors[i];
                var matchingTile = convMaster.ConveyorTile.FirstOrDefault(t => t.Tile.X == tempTile.Tile.X && t.Tile.Y == tempTile.Tile.Y);
                if (matchingTile is null) continue;
                tempTile.NextConveyorTile = matchingTile;
            }
        }


        await PlayerPathFinding();




        /*
        https://enzisoft.wordpress.com/2016/03/12/factorio-in-unityc-goal-reached/ 
        https://www.factorio.com/blog/post/fff-148
        https://docs.flexsim.com/en/19.2/ConnectingFlows/Conveyors/CreatingConveyorLogic/
        https://github.com/Club559/FactoryMod/tree/master/FactoryMan/Items
        https://github.com/taku686/BeltConveyor
        https://github.com/skulifh/conveyor_belt_simulator/blob/master/G12-Robust-Software-Systems/Simulation/Initiator.cs 

        We should probably have a list of conveyors.
        but maybe it should be saved in state.
        So we have two temp spaces
        When the second one is filled, we create a conveyor segment according to a path. If no path exists, abandon.
        Later on I need to have stairs that go over the conveyor so people can get over it.
        That will probably be a bit troublesome. It means that I have to add something else to the path-finding, since right now I only have a list of paths that substitute from a cell. I would need supplemental.
        What if conveyors can also be elevated so humans could walk past it?
        Then I also need to spawn items on the conveyor. 
        A finished conveyor has an ordered list of tiles. Maybe also references to items on it. 
        I guess tiles also need to have a list of conveyorTiles.
        I also need to have a direction for each of them.
        In the end, a conveyor should be composed of segments.
        Rotating items as they go around bends should be fun.
        I also need to think about how this will run on a server. (timeStep Event = any item leaving/entering conveyor/segment)
        */
    }



    private async void HandleRightClickPlayerMode(Tile tile)
    {
        //ToDo: Make sure right-clicking source/destination doesn't put it in a bad state.
        var keys = PlayerDictionary.Keys.ToArray();
        for (var index = 0; index < keys.Length; index++)
        {
            var key = keys[index];

            var kvp = PlayerDictionary[key];
            if (kvp.Destination is not null && !kvp.Destination.IsPassable) kvp = (kvp.Source, null);
            if (kvp.Source is not null && !kvp.Source.IsPassable) kvp = (null, kvp.Destination);
            if (kvp.Destination == tile)
            {
                PlayerDictionary[key] = (kvp.Source, null);
                tile.TileRole = TileRole.Nothing;
                return;
            }

            if (kvp.Source == tile)
            {
                PlayerDictionary[key] = (null, kvp.Source);
                tile.TileRole = TileRole.Nothing;
                return;
            }
        }

        if (tile.TileRole == TileRole.Nothing && tile.IsPassable)
        {
            if (PlayerDictionary[CurrentPlayer].Source is null)
            {
                PlayerDictionary[CurrentPlayer] = (tile, PlayerDictionary[CurrentPlayer].Destination);
                tile.TileRole = TileRole.Source;
                if (AlwaysPath) await PlayerPathFinding();
                return;
            }

            if (PlayerDictionary[CurrentPlayer].Destination is null)
            {
                PlayerDictionary[CurrentPlayer] = (PlayerDictionary[CurrentPlayer].Source, tile);
                tile.TileRole = TileRole.Destination;
                if (AlwaysPath) await PlayerPathFinding();
                return;
            }
        }

    }

    public async Task Tick(Point? point, bool leftClicked)
    {
        GetHoverElements(point);
        if (!Paused)
        {
            RandomlyAddItem();
            _tickCounter++;
            if (_tickCounter >= 5)
            {
                Movement();
                _tickCounter = 0;
            }
        }
        if (leftClicked) { await HandleLeftClick(point); }
        else { AlreadyClicked.Clear(); }
    }

    public void RandomlyAddItem()
    {
        if (!Conveyors.Any() || rand.NextDouble() > 0.1) { return; }

        var conveyorIndex = rand.Next(0, 0);// Conveyors.Count);
        var conveyor = Conveyors[conveyorIndex];
        var conveyorTile = conveyor.ConveyorTile.First();
        if (conveyorTile.Items.Any()) return;
        var item = new Item() { X = rand.Next(0, MaxCellNumber), Y = rand.Next(0, MaxCellNumber), ConveyorTile = conveyorTile };
        conveyorTile.Items.Add(item);
        conveyor.Items.Add(item);
        Items.Add(item);
    }

    public void Movement()
    {
        //ToDo: Actually I need to go through items according to who is furthest down the line so I can account for collisions.
        //Maybe it would be the segment * 10 + (_maxCellNumber * the tile direction (if x=3 and direction = (1,3), then it's the first that would be checked.). I'd look at highest first.
        //ToDo: What if I kept the left and right hand sides separate? Just have to define left/right side of conveyor.
        for (var index = 0; index < Items.Count; index++)
        {
            var item = Items[index];
            item.X -= item.ConveyorTile.Direction.x;
            item.Y -= item.ConveyorTile.Direction.y;

            ConveyorTile nextConveyor = item.ConveyorTile.NextConveyorTile;
            if (item.ConveyorTile.NextConveyorTile is null)
            {
                DeleteItem(item);
                continue;
            }

            //ToDo: I can replace almost all of this logic by holding NextConveyor tile in ConveyorTile
            var conveyor = item.ConveyorTile.Conveyor;
            var currentConveyorTile = item.ConveyorTile;



            if (item.X < 0)
            {
                if (nextConveyor is null) { DeleteItem(item); continue; }
                currentConveyorTile.Items.Remove(item);
                item.ConveyorTile = nextConveyor;
                nextConveyor.Items.Add(item);
                item.X = MaxCellNumber - 1;
                continue;
            }

            if (item.X > MaxCellNumber)
            {
                if (nextConveyor is null) { DeleteItem(item); continue; }
                currentConveyorTile.Items.Remove(item);
                item.ConveyorTile = nextConveyor;
                nextConveyor.Items.Add(item);
                item.X = 0;
                continue;
            }

            if (item.Y < 0)
            {
                if (nextConveyor is null) { DeleteItem(item); continue; }
                currentConveyorTile.Items.Remove(item);
                item.ConveyorTile = nextConveyor;
                nextConveyor.Items.Add(item);
                item.Y = MaxCellNumber - 1;
                continue;
            }

            if (item.Y > MaxCellNumber)
            {
                if (nextConveyor is null) { DeleteItem(item); continue; }
                currentConveyorTile.Items.Remove(item);
                item.ConveyorTile = nextConveyor;
                nextConveyor.Items.Add(item);
                item.Y = 0;
                continue;
            }

        }
    }

    public void DeleteItem(Item item)
    {
        Items.Remove(item);
        item.ConveyorTile.Items.Remove(item);
    }

}

public class Item
{
    //ToDo: Maybe this should also have the last velocity so it can continue to go to the left/right side of conveyor
    public int X;
    public int Y;
    public ConveyorTile ConveyorTile;
}

public class ConveyorTile
{
    public (int x, int y) Direction;
    public Tile Tile;
    public List<Item> Items = new();
    public Conveyor Conveyor;
    public ConveyorTile NextConveyorTile;
}

public class Conveyor
{
    //ToDo: I need to make it so I can serialize this so it can be saved in a map string.
    //ToDo: Since this should be part of the map string, it should also be held in State.
    public int Id;
    public List<ConveyorTile> ConveyorTile { get; set; } = new();
    public List<Item> Items = new();
}

public class Tile
{
    public string Name { get; set; }
    public bool IsPassable;
    public readonly int Id;
    internal TileRole TileRole = TileRole.Nothing;
    public bool IsPartOfSolution;
    public int X;
    public int Y;
    public int ChunkId = -1;
    public string Description => Name;

    public Tile(int x, int y, bool isPassable, int id)
    {
        Id = id;
        X = x;
        Y = y;
        IsPassable = isPassable;
        Name = $"{x},{y}";
    }
}

public enum TileRole { Nothing = 0, Source = 1, Destination = 2, Conveyor = 3 }

public class State
{
    public int X;
    public int Y;
    private readonly IStatePersistence _sp;
    private Tile[,] _tileGrid;
    public List<Tile> Tiles { get; set; } = new();
    public int TileSize { get; }
    public Tile[,] TileGrid { get => _tileGrid; set => _tileGrid = value; }

    public State(int x, int y, int tileSize, IStatePersistence sp)
    {
        X = x;
        Y = y;
        _sp = sp;
        TileSize = tileSize;
        TileGrid = new Tile[x, y];
        SetTiles();
    }

    private void SetTiles()
    {
        var i = 0;
        for (var x = 0; x < X; x++)
        {
            for (var y = 0; y < Y; y++)
            {
                var tempTile = new Tile(x, y, true, i++);
                TileGrid[x, y] = tempTile;
                Tiles.Add(tempTile);
            }
        }
    }

    public string SetupMapString() => _sp.SetupMapString(ref X, ref Y, ref _tileGrid);

    public (int X, int Y, string[] mapStringArray, bool Success) UploadMapString(string mapString) => _sp.UploadMapString(mapString);
}

