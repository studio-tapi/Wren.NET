class Foo {
  construct new() {}
  +(other) { "Foo " + other }
}

System.print(Foo.new() + "value") // expect: Foo value

// TODO: Other expressions following a constructor, like new Foo.bar("arg").
