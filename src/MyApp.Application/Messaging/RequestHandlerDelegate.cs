namespace MyApp.Application.Messaging;

public delegate Task<TResponse> RequestHandlerDelegate<TResponse>();
