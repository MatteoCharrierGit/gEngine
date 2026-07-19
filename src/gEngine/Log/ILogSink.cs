namespace gEngine.Log;

/// <summary>
/// Dove finisce un messaggio già deciso: lo stdout, un pannello dell'editor, un file.
///
/// PERCHÉ è separata da <see cref="ILogger"/>, che a prima vista fa la stessa cosa: sono i due
/// lati opposti dello stesso tubo. <c>ILogger</c> è la porta di chi <b>produce</b> ("scrivi
/// questo"), <c>ILogSink</c> è la porta di chi <b>consuma</b> ("ecco cosa è stato scritto").
/// Finché il logger scriveva solo su console le due combaciavano e una sola interfaccia
/// bastava; con una console in-editor i consumatori diventano due e devono ricevere
/// <b>entrambi</b> lo stesso messaggio, senza che chi logga sappia quanti sono.
///
/// Un sink riceve il <see cref="LogMessage"/> <b>già filtrato</b> per livello: la soglia è una
/// sola e sta nel <see cref="Logger"/>. Un sink che rifiltrasse per conto suo darebbe due
/// pannelli con contenuti diversi e nessun posto dove leggere qual è la regola.
///
/// ⚠️ <c>in</c> e non per valore: <see cref="LogMessage"/> è uno struct e i sink sono N —
/// senza, ogni messaggio si copierebbe una volta per sink.
/// </summary>
public interface ILogSink
{
    void Write(in LogMessage message);
}
