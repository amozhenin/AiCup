package com.google.common.collect;

import com.google.common.annotations.GwtCompatible;
import java.lang.annotation.Annotation;
import java.lang.annotation.Documented;
import java.lang.annotation.Retention;
import java.lang.annotation.RetentionPolicy;
import java.lang.annotation.Target;

@GwtCompatible
@Documented
@Retention(RetentionPolicy.RUNTIME)
@Target({java.lang.annotation.ElementType.FIELD})
@interface GwtTransient {}


/* Location:              D:\Projects\AiCup\CodeRacing\local-runner\local-runner.jar!\com\google\common\collect\GwtTransient.class
 * Java compiler version: 6 (50.0)
 * JD-Core Version:       0.7.1
 */