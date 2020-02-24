module EvalNetHelper
open SharedTypes
open EvalTypes

let extractLogicLevel logicLvlOpt = 
     match logicLvlOpt with
        |Some logicLvl -> logicLvl
        |None -> failwith "Logic Level has not been assigned yet"

let extractNetMap evalNet =
    match evalNet with
    |EvalBus y
    |EvalWire y -> y

let createNewBusMap (a,b) initVal = 
    [a..b]
    |> List.map (fun x -> (x, initVal)) 
    |> Map

let createNewBus (a, b) initVal = 
        createNewBusMap (a,b) initVal |> EvalBus

let padLogicLvlListToLength logicLvlLst fullLength =

    if List.length logicLvlLst >= fullLength
    then logicLvlLst
    else
        [1..(fullLength - (List.length logicLvlLst))]
        |> List.map (fun _ -> Low)
        |> List.append logicLvlLst


//caller can supply None for slice indicies to update whole bus
let updateBus bus sliceIndices newLogicLevels = 
    let (a,b) = 
        match sliceIndices with
        |Some (x,y) -> (x,y)
        |None -> 
            let wireIndices = 
                Map.toList bus
                |> List.map (fst)
            (List.min wireIndices, List.max wireIndices)

    if abs(b - a + 1) <> (List.length newLogicLevels) || not (Map.containsKey b bus) || not (Map.containsKey a bus)
    then failwith "Cannot update bus, error in given bus slice"
    else
        bus
        |> Map.map (fun key oldVal ->
            if key >= a && key <= b
            then Some newLogicLevels.[key - a]
            else oldVal
            )

let updateWire wire newLogicLevel = Map.add 0 (Some newLogicLevel) wire

let rec intToLogicLevelList uint logicLvlLst =
    if uint = 0
    then logicLvlLst
    else
        if (uint % 2) = 1
        then intToLogicLevelList (uint / 2) (logicLvlLst @ [High])
        else intToLogicLevelList (uint / 2) (logicLvlLst @ [Low])

let logicLevelsToint logicLvlLst = 
    let getDecimalValue bitPos bit = 
        if bit = High
        then int ((float 2) ** (float bitPos))
        else 0
    List.mapi getDecimalValue logicLvlLst
    |> List.reduce (+)

let isNetEvaluated evalNet = 
    let netMap = extractNetMap evalNet
    Map.fold (fun netEvaluated _ logicLevelOpt ->
        if not netEvaluated
        then false
        else
            match logicLevelOpt with
            |Some logicLvl -> true
            |None -> false) true netMap