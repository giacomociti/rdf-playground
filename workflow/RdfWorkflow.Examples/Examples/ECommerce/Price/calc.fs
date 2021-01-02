module ECommerce.Price

open Utils

let (|EUR|USD|Other|) (offer: Schema.Offer) =
    match offer.PriceCurrency.Single with
    | "EUR" -> EUR offer
    | "USD" -> USD offer
    | _ -> Other offer

let (|Expensive|_|) (offer: Schema.Offer) =
    let price = offer.Price.Single
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
        printfn "promote %s to rich customers" offer.Gtin.Single
    | _ -> ()

let sendOffers data =
    Schema.Offer.Get data
    |> Seq.iter sendOffer


