module Utils

open VDS.RDF
open VDS.RDF.Query
open VDS.RDF.Update
open VDS.RDF.Parsing
open VDS.RDF.Shacl
open System.IO
open System.Collections.Generic
open VDS.RDF.Query.Inference

type System.Collections.Generic.IEnumerable<'a> with
    member this.Single = Seq.exactlyOne this

type INode with
    member this.Uri = (this :?> IUriNode).Uri
    member this.Types = 
        let uri = UriFactory.Create RdfSpecsHelper.RdfType
        let typeNode = this.Graph.CreateUriNode uri
        this.Graph.GetTriplesWithSubjectPredicate(this, typeNode) 
        |> Seq.map (fun x -> x.Object.Uri)

type IFactory =
   abstract member CreateQuery: string -> SparqlQuery
   abstract member CreateUpdate: string -> SparqlParameterizedString
   abstract member CreateShaclShape: string -> ShapesGraph


let parseTurtleFile path =
    let graph = new Graph()
    FileLoader.Load(graph, path)
    graph

let parseTurtleText text =
    let graph = new Graph()
    TurtleParser().Load(graph, new StringReader(text))
    graph

let factory (dict: IDictionary<_,string>) =
    let queryParser = SparqlQueryParser()

    { new IFactory with
        member _.CreateQuery name =
            dict.[name] |> queryParser.ParseFromString
           
        member _.CreateUpdate name =
           dict.[name] |> SparqlParameterizedString

        member _.CreateShaclShape name =
           new ShapesGraph(dict.[name] |> parseTurtleText)
    }

let construct (query: SparqlQuery) (input: IGraph) =
    input.ExecuteQuery query :?> IGraph

let ask (query: SparqlQuery) (input: IGraph) =
    (input.ExecuteQuery query :?> SparqlResultSet).Result

let shacl (shapes: ShapesGraph) (input: IGraph) =
    shapes.Validate input
    
let infer (input: IGraph) =
    RdfsReasoner().Apply(input)

let select (query: SparqlQuery) (input: IGraph) =
    input.ExecuteQuery query :?> SparqlResultSet

let remoteUpdate (endpoint: RemoteUpdateProcessor) (commands: SparqlUpdateCommandSet) =
    endpoint.ProcessCommandSet commands

let setArgs variables (sparql: SparqlParameterizedString) (args: SparqlResult) =
    for v in variables do sparql.SetVariable(v, args.[v])
    sparql.ToString()

let getCommands (sparql: SparqlParameterizedString) (args: SparqlResultSet) =
    args.Results 
    |> Seq.map (setArgs args.Variables sparql)
        
