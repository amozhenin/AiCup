package com.google.common.collect;

import com.google.common.annotations.GwtCompatible;

@GwtCompatible
public abstract class ForwardingObject
{
  protected abstract Object delegate();
  
  public String toString()
  {
    return delegate().toString();
  }
}


/* Location:              D:\Projects\AiCup\CodeRacing\local-runner\local-runner.jar!\com\google\common\collect\ForwardingObject.class
 * Java compiler version: 6 (50.0)
 * JD-Core Version:       0.7.1
 */