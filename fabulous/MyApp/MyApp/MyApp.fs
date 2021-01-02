// Copyright 2018-2019 Fabulous contributors. See LICENSE.md for license.
namespace MyApp

open System.Diagnostics
open Fabulous
open Fabulous.XamarinForms
open Fabulous.XamarinForms.LiveUpdate
open Xamarin.Forms
open VDS.RDF.Query

module Utils =

    let shuffle (a: _[]) =
        let swap x y =
            let tmp = a.[x]
            a.[x] <- a.[y]
            a.[y] <- tmp
        let rand = new System.Random()
        Array.iteri (fun i _ -> swap i (rand.Next(i, Array.length a))) a
        a

module Domain =
    type City = { Name: string; Population: int}

    type Question = City array

module KnowledgeBase =
    let run query =
        let uri = System.Uri "https://query.wikidata.org/sparql"
        let endpoint = SparqlRemoteEndpoint(uri, UserAgent = ".Net Client")
        endpoint.QueryWithResultSet(query)

    open Iride
    open Domain

    type PopulationQuery = SparqlQueryProvider<"population.rq">

    let getCities limit =
        async { 
            do! Async.SwitchToThreadPool()
            return 
                PopulationQuery.GetText(INT_Limit = limit)
                |> run
                |> Seq.map PopulationQuery.Result
                |> Seq.map (fun x -> { Name = x.LIT_cityLabel; Population = int x.NUM_population})
                |> Seq.toArray
                |> Utils.shuffle
        }
       

module App = 
    
    open KnowledgeBase
    open Domain
    
    type State = Init | Loading | Asking | CorrectAnswer of string | WrongAnswer of string | Finished

    type Model = 
      { 
        State: State
        Questions: Question array
        Step: int
        Score: int } with

        member this.Current =
            if this.Step <= this.Questions.Length
            then this.Questions.[this.Step-1] |> Array.toList
            else []

        member this.IsCorrect(answer) =  
            answer = (this.Current |> List.maxBy (fun x -> x.Population)).Name


    type Msg = 
        | Start
        | Ready of City array
        | Answer of string
        | Next

    let chunkSize = 3
    let numberOfQuestions = 10

    let initCommand = 
        async { 
           let! cities = getCities (chunkSize * numberOfQuestions) 
           return cities |> Ready }
        |> Cmd.ofAsyncMsg
       
    let initModel = { State = Init
                      Questions = [||]
                      Step = 0
                      Score = 0 }

    let init () = initModel, Cmd.none

    

    let update msg model =
        match msg with
        | Start -> { model with State = Loading }, initCommand
        | Ready cities -> 
            let chunks = 
                cities
                |> Array.chunkBySize chunkSize 
                |> Array.filter (fun x -> x.Length = chunkSize)
            { State = Asking; Questions = chunks; Step = 1; Score = 0}, Cmd.none
        | Next ->
            if model.Step = model.Questions.Length
            then { model with State = Finished }, Cmd.none
            else { model with State = Asking; Step = model.Step+1 }, Cmd.none
        | Answer x -> 
            ( if model.IsCorrect x
            then { model with State = CorrectAnswer x; Score = model.Score+1}
            else { model with State = WrongAnswer x}), Cmd.none
           
        
    let view (model: Model) dispatch =
        let elements = 
            match model.State with
            | Init -> [
                View.Label(text = "Guess the city with more inhabitants", horizontalOptions = LayoutOptions.Center, width=200.0, horizontalTextAlignment=TextAlignment.Center)
                View.Button(text = "Start!", command = fun () -> dispatch Start)
              ]
            | Loading -> [
                View.Label(text = "Loading from Wikidata...", horizontalOptions = LayoutOptions.Center, width=200.0, horizontalTextAlignment=TextAlignment.Center)
                View.ActivityIndicator(isRunning = true)
              ]
            | Asking -> [ 
                View.Label(text = sprintf "question %i - score %i" model.Step model.Score, horizontalOptions = LayoutOptions.Center, width=200.0, horizontalTextAlignment=TextAlignment.Center)
                View.ListView(
                    items = (model.Current |> List.map (fun x -> View.TextCell(x.Name))),
                    selectedItem = Some -1,
                    itemSelected = (fun idx ->
                        match idx with
                        | Some i -> dispatch (Answer (model.Current.[i].Name))
                        | None -> ())
                )
              ]
            | CorrectAnswer x -> [ 
                View.Label(text = sprintf "Correct answer: %s" x, horizontalOptions = LayoutOptions.Center, width=200.0, horizontalTextAlignment=TextAlignment.Center)
                View.ListView(
                    items = (model.Current |> List.map (fun x -> View.TextCell(sprintf "%s has %i inhabitants" x.Name x.Population)))
                )
                View.Button("Next", command = fun () -> dispatch Next)
              ]
            | WrongAnswer x -> [ 
                View.Label(text = sprintf "Wrong answer: %s" x, horizontalOptions = LayoutOptions.Center, width=200.0, horizontalTextAlignment=TextAlignment.Center)
                View.ListView(
                    items = (model.Current |> List.map (fun x -> View.TextCell(sprintf "%s has %i inhabitants" x.Name x.Population)))
                )
                View.Button(text = "Next", command = fun () -> dispatch Next)
              ]
            | Finished -> [
                View.Label(text = sprintf "Final Score is %i" model.Score, horizontalOptions = LayoutOptions.Center, width=200.0, horizontalTextAlignment=TextAlignment.Center)
                View.Button(text = "Start Again", command = fun () -> dispatch Start)
              ]
        View.ContentPage(content = View.StackLayout(padding = Thickness 20.0, verticalOptions = LayoutOptions.Center, children = elements))

    // Note, this declaration is needed if you enable LiveUpdate
    let program = XamarinFormsProgram.mkProgram init update view

type App () as app = 
    inherit Application ()

    let runner = 
        App.program
#if DEBUG
        |> Program.withConsoleTrace
#endif
        |> XamarinFormsProgram.run app

#if DEBUG
    // Uncomment this line to enable live update in debug mode. 
    // See https://fsprojects.github.io/Fabulous/Fabulous.XamarinForms/tools.html#live-update for further  instructions.
    //
    do runner.EnableLiveUpdate()
#endif    

    // Uncomment this code to save the application state to app.Properties using Newtonsoft.Json
    // See https://fsprojects.github.io/Fabulous/Fabulous.XamarinForms/models.html#saving-application-state for further  instructions.
#if APPSAVE
    let modelId = "model"
    override __.OnSleep() = 

        let json = Newtonsoft.Json.JsonConvert.SerializeObject(runner.CurrentModel)
        Console.WriteLine("OnSleep: saving model into app.Properties, json = {0}", json)

        app.Properties.[modelId] <- json

    override __.OnResume() = 
        Console.WriteLine "OnResume: checking for model in app.Properties"
        try 
            match app.Properties.TryGetValue modelId with
            | true, (:? string as json) -> 

                Console.WriteLine("OnResume: restoring model from app.Properties, json = {0}", json)
                let model = Newtonsoft.Json.JsonConvert.DeserializeObject<App.Model>(json)

                Console.WriteLine("OnResume: restoring model from app.Properties, model = {0}", (sprintf "%0A" model))
                runner.SetCurrentModel (model, Cmd.none)

            | _ -> ()
        with ex -> 
            App.program.onError("Error while restoring model found in app.Properties", ex)

    override this.OnStart() = 
        Console.WriteLine "OnStart: using same logic as OnResume()"
        this.OnResume()
#endif


