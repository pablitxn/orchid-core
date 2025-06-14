using System;

namespace Domain.Exceptions;

public class EntityNotFoundException : Exception
{
    public EntityNotFoundException() : base() { }
    
    public EntityNotFoundException(string message) : base(message) { }
    
    public EntityNotFoundException(string message, Exception innerException) : base(message, innerException) { }
    
    public EntityNotFoundException(string entityName, object id) 
        : base($"Entity '{entityName}' with id '{id}' was not found.") { }
}