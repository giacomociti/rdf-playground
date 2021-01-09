Maybe RDF
====================
## A provoking claim
As a software anarchitect, I like to challenge the status quo:
I propose to use [RDF](https://www.w3.org/TR/rdf11-primer/)
and [SPARQL](https://www.w3.org/TR/sparql11-overview/)
for the core domain logic of business applications.

Many business applications consist of simple workflows to process rich information.
My claim is that RDF and SPARQL are ideal to model and process such information
while a workflow engine can orchestrate the processing steps.

## Cheap philosophy
OO classes _hide_ information. They do so to pursue modularity and tame complexity,
but [hiding information](https://en.wikipedia.org/wiki/Information_hiding) may not be
a good idea when building information processing applications.
More concrete structures, like algebraic data types, can model information explicitly
and in fact are becoming popular for domain modeling.

Still, I contend that a logical framework like RDF shines at representing knowledge about a domain.
Rich Hickey's provoking [talks](https://www.youtube.com/watch?v=YR5WdGrpoug&list=PLZdCLR02grLrEwKaZv-5QbUzK0zGKOOcr) may upset my F# friends, but I think he has a point: explicit, precise data types may lead to rigid designs (to be clear, [this article](https://lexi-lambda.github.io/blog/2020/01/19/no-dynamic-type-systems-are-not-inherently-more-open/) explains that the culprit for a rigid design is not the type system).

In domain modeling, common advice is to focus on functions and not on data: we should describe the _dynamic_ behavior of a system rather than static information.
This applies both to OO (classes are collections of functions) and FP (pure functions still have a
dynamic, computational sense even though we like to think of them as static input-output mappings).

Often this advice is neglected. Partly for historical reasons stemming from the dominance of relational databases.
Partly because the value of many business applications lies more in the data than in their processing steps.
My endorsement of RDF is limited to this kind of applications, for which other declarative approaches,
SQL-like or Prolog-like, may work as well.

## Proof of Concept
I admit this is cheap philosophy and my claim is not backed by real world experience, so I decided to get a feel of what it means to build an application with a core domain based on RDF.
As a proof of concept, I hacked a [toy](https://github.com/giacomociti/rdf-playground/blob/master/workflow/RdfWorkflow/Workflow.fs) workflow engine in a few lines of F# code.
It orchestrates the steps of workflow definitions like the following one (expressed as RDF in Turtle notation):

```ttl
@prefix w: <http://workflow.org/> .
@prefix : <http://example.org/> .

:search a w:Workflow ;
    w:startAt :validation .
:validation a w:AskStep ;
    w:sparqlQuery "validation.rq" ;
    w:nextOnTrue :retrieval ;
    w:nextOnFalse :ko .
:retrieval a w:ConstructStep ;
    w:sparqlQuery "retrieval.rq" ;
    w:next :ok .
:ko a w:FinalStep ;
    w:success false .
:ok a w:FinalStep ;
    w:success true .
```

The workflow accepts RDF input like:

```ttl
@prefix : <http://example.org/> .

[ a :SearchRequest ;
    :keyword "logic", "software" ] .
```

and the workflow steps use the [dotNetRDF](https://www.dotnetrdf.org/) library to process information with SPARQL:
_ASK_ queries for branching (although for validation we may also use something more specific like _SHACL_):

```sparql
# validation.rq
prefix : <http://example.org/>

ASK
WHERE {
    ?request a :SearchRequest ;
        :keyword ?keyword .
    FILTER (strlen(?keyword) > 3)
}
```

and _CONSTRUCT_ queries to transform and merge information:

```sparql
# retrieval.rq
prefix : <http://example.org/>

CONSTRUCT {
    ?result :about ?keyword .
}
WHERE {
    ?request a :SearchRequest ;
        :keyword ?keyword .
    SERVICE <https://mytriples/sparql> {
        ?result :about ?keyword .
    }
}
```

Query processing happens in memory but we can use also RDF databases (triplestores) for persistent storage of information.
Federated queries (with the SERVICE keyword) allow to relate information in memory with information stored in RDF databases.

Of course real applications interact with different kinds of databases and other infrastructure (queues, APIs...)
so our workflow engine needs to plug in custom adapter code for such interactions
(and for when data processing is complex enough and requires a real programming language).
But, overall, RDF provides a great data model with standard and uniform tools to process, persist and serialize information with no impedance mismatch.

A mixed paradigm
================
Most programmers (including me) are scared of building applications using something other than
their favourite programming language. Filling in the gaps of some 'bubbles and arrows' workflow framework can be frustrating and painful, especially when such tools are built to appeal managers, selling the illusion to create applications with almost no programming skills.
Therefore, it's fundamental a smooth integration of declarative RDF processing with regular programming.
Type providers in [Iride](https://github.com/giacomociti/iride) can help to bridge RDF information with processing code.

The following `sendOffers` function can be plugged as a custom step into a workflow.
It takes an instance of `IGraph` as input and access its information through types
generated from an RDF schema by `GraphProvider`.
A concern may be that external libraries like dotNetRDF pollute our domain.
But the `IGraph` interface is much like `ICollection` or `IDictionary` from the base library.
Purists would ban all of them but in practice they appear routinely in domain logic.

```fsharp
open Iride

type Schema = GraphProvider<Schema="schema.ttl">

let (|EUR|USD|Other|) (offer: Schema.Offer) =
    match Seq.exactlyOne offer.PriceCurrency with
    | "EUR" -> EUR offer
    | "USD" -> USD offer
    | _ -> Other offer

let (|Expensive|_|) (offer: Schema.Offer) =
    let price = Seq.exactlyOne offer.Price
    match offer with
    | EUR _ ->
        if price > 200m
        then Some (Expensive offer)
        else None
    | USD _ ->
        if price > 250m
        then Some (Expensive offer)
        else None
    | Other _ -> None

let sendOffer = function
    | Expensive offer ->
        let gtin = Seq.exactlyOne offer.Gtin
        printfn "promote %s to rich customers" gtin
    | _ -> ()

let sendOffers (data: VDS.RDF.IGraph) =
    Schema.Offer.Get data
    |> Seq.iter sendOffer
```

Notice how provided types help navigating information but lack precision.
`Price`, `PriceCurrency` and `Gtin` are sequences because RDF allows multiple property values.
Here, the application is assuming there is a single value for all of them
(possibly relying on a previous SHACL validation step, because the schema only describes a domain, imposing no constraint).

In F#, we enjoy the kind of precision given by union types.
I argue their strength is more in taming cyclomatic complexity rather than in information modeling.
By providing exaustive case matching (like active patterns in the example), union types implicitly
constrain the processing paths, hence they pertain more to the dynamic aspect of a system.

## Conclusion
Type Providers and data related technologies like RDF are expected to live inside adapters at the
boundaries of applications, far removed from the core domain logic.
I argue in favor of admitting them inside the core of information-based applications.
Although my aim is mainly thought-provoking, I really hope to see some ideas from declarative, logic based
paradigms, percolate into mainstream programming, much like what happened with functional programming
permeating OO languages.






