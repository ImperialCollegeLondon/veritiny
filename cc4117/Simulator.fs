module Simulator
open SharedTypes
open SimulationTypes
open Helper
open SynchronousBlocks
open CombEval


/// Extract synchronous nets from cLst and initialize them to Low
let returnSyncNets (cLst: Connection List) = 
    
    let findAllSync (cLst:Connection List) =
        let rec findSync acc gLst =
            match gLst with
            | hd::tl -> 
                if fst hd then 
                    findSync (acc @ [hd]) tl
                else 
                    findSync acc tl
            | [] -> 
                acc
        let getSyncFromConnection (cIn: Connection) =
            cIn 
            |> extractGenNetLsts 
            |> opOnTuple (findSync []) 
            |> (fun (a, b) -> a @ b)
        List.collect getSyncFromConnection cLst
    
    let initializeSync (gNet: GeneralNet) = 
        let newMapLen = gNet |> extractNet |> netSize 
        updateGenNet gNet (createNewMap newMapLen)
        
    cLst 
    |> findAllSync 
    |> List.distinct 
    |> List.map initializeSync 


let getInitMap (currentInputs:Map<NetIdentifier,Net>) (syncNetMap:Map<NetIdentifier,Net>) =
    updateMap currentInputs syncNetMap


/// Convert to Block list for evaluation 
let cLstToBlockLst (cLst: Connection List) : Block list =
    List.map (fun (megaBlock, a, b) -> (megaBlock, gLstToMap a, gLstToMap b)) cLst


// Seperate sync/async megablocks 
let seperateMegaBlocks (bLst: Block list) =
    let checkIfSyncBlock (bIn: Block) =
        let (megaBlock), _, _ = bIn
        List.contains megaBlock syncMegaLst
    let rec seperate lstA lstB lst =
        match lst with
        | hd::tl ->
            if checkIfSyncBlock hd then
                seperate (lstA @ [hd]) lstB tl
            else
                seperate lstA (lstB @ [hd]) tl
        | [] -> lstA, lstB
    seperate [] [] bLst // -> (syncBLst, asyncBLst)

/// Advance to next clock cycle, return synchronous states
let advanceState (initMap: Map<NetIdentifier,Net>) (asyncBLst: Block list) (syncBLst: Block list) (tLst: TLogic List) = // map<NetIdentifier, Net> -> all info of sync or not is removed

    // Check if otherMap is a subset of reference map
    let checkIfInMap (refMap: Map<'a,'b>) (otherMap: Map<'a,'b>) =
        let rec listCompare m lst =
            match lst with 
            | hd::tl when Map.containsKey hd m->
                listCompare m tl
            | [] -> true
            | _ -> false
        otherMap 
        |> Map.toList 
        |> List.map fst 
        |> listCompare refMap

    // Take values from acc given keys of mapIn
    let takeFromMap (acc:Map<NetIdentifier,Net>) (mapIn:Map<NetIdentifier,Net>) =
        let netIds = mapIn |> Map.toList |> List.map fst
        let netList= List.map (fun x -> acc.[x]) netIds
        List.zip netIds netList |> Map.ofList

    let getTLogic (mBlock: Megablock) (tLst:TLogic List)=
        let (Name str) = mBlock
        let checker s (tLog: TLogic): bool = 
            s = tLog.Name
        //List.tryFind (checker str) tLst 
        List.find (checker str) tLst

    let getOutput (mapIn:Map<NetIdentifier,Net>) (origNames:Map<NetIdentifier,Net>) (tLog:TLogic) =
        let renameForEval (mapIn:Map<NetIdentifier,Net>)  =
        //type Expression = (Operator * NetIdentifier list * NetIdentifier list)
            let nets = mapIn |> Map.toList |> List.map snd
            let tempKeys = tLog.Inputs 
            List.zip tempKeys nets |> Map.ofList

        let renameEvalOut (origNames:Map<NetIdentifier,Net>) (evalMap:Map<NetIdentifier,Net>)   =
            let nets = evalMap |> Map.toList |> List.map snd
            let realKeys = origNames |> Map.toList |> List.map fst
            List.zip realKeys nets |> Map.ofList

        mapIn |> renameForEval |> evaluateModuleWithInputs tLog |> renameEvalOut origNames
    
    // output of this function will return "mapOfVals"
    let rec simulateAsync (acc:Map<NetIdentifier,Net>) (asyncBLst: Block list) =
        match asyncBLst with 
        | (mBlock, mapIn, mapOut)::rest when checkIfInMap acc mapIn ->
            let tLog = getTLogic mBlock tLst
            // take values from acc using keys of mapIn
            let mapIn' = takeFromMap acc mapIn
            let acc' = getOutput mapIn' mapOut tLog|> updateMap acc
            simulateAsync acc' rest
        | hd::rest ->             
            simulateAsync acc (rest @ [hd])
        | [] -> 
            acc
        | _ -> failwithf "nani? how did that happen"

    let rec simulateSync (acc:Map<NetIdentifier,Net>) (refMap:Map<NetIdentifier,Net>) (syncBLst: Block list) =
        match syncBLst with
        | (mBlock, mapIn, mapOut)::rest when List.contains mBlock syncMegaLst->
            // takes values from refMap using keys of mapIn
            let mapIn' = takeFromMap refMap mapIn
            let acc' = updateDFF mapIn' mapOut |> updateMap acc
            simulateSync acc' refMap rest
        | [] -> acc 
        | _ -> failwithf "other megablocks not supported yet"

    let mapOfVals = simulateAsync initMap asyncBLst
    printfn "States after asynchronous evaluation: \n %A" mapOfVals
    let nextState = simulateSync (Map []) mapOfVals syncBLst
    printfn "Synchronous states after synchronous evaluation: \n %A" nextState
    nextState

let simulate (lstOfInputs: GeneralNet List list) (cLst:Connection list) (tLst: TLogic list)=
    // Initialize/setup
    let initialSyncNetMap = returnSyncNets cLst |> gLstToMap
    let bLst = cLstToBlockLst cLst
    let (syncBLst, asyncBLst) = seperateMegaBlocks bLst

    // Keep advancing state until the lst of inputs are exhausted
    let rec advanceMore prevState (lstOfInputs: GeneralNet list list) =
        match lstOfInputs with
        | currentInputs::rest -> 
            let initMap = getInitMap (gLstToMap currentInputs) prevState
            let nextState = advanceState initMap asyncBLst syncBLst tLst
            advanceMore nextState rest
        | [] -> prevState

    advanceMore initialSyncNetMap lstOfInputs
    
        

